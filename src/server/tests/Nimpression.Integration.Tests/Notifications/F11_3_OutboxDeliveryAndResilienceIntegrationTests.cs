using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Notifications.Outbox;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class F11_3_OutboxDeliveryAndResilienceIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private MailpitTestClient _mailpit = null!;
    private TestDateTimeProvider _dateTimeProvider = null!;

    public F11_3_OutboxDeliveryAndResilienceIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _mailpit = _fixture.CreateMailpitClient();

        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();

            if (!await db.EmailTemplates.AnyAsync(t => t.Key == NotificationTemplateKeys.ServiceDueReminder))
            {
                var template = new EmailTemplate(
                    Guid.NewGuid(),
                    NotificationTemplateKeys.ServiceDueReminder,
                    "Vehicle Service Due - {{VehicleRego}}",
                    "车辆保养提醒 - {{VehicleRego}}",
                    "Vehicle {{VehicleRego}} reached {{CurrentOdometer}} km.",
                    "车辆 {{VehicleRego}} 里程达到 {{CurrentOdometer}} 公里。",
                    true);

                await db.EmailTemplates.AddAsync(template);
                await db.SaveChangesAsync();
            }
        }

        _dateTimeProvider = TestDateTimeProvider.FromNzDate(2026, 8, 30);
        await _mailpit.ClearAllMessagesAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _mailpit?.Dispose();
    }

    [Fact]
    public async Task OutboxDelivery_ResilientToProcessKillAndRestart_DeliversPendingEmailsAfterRestart()
    {
        // ── Phase 1: 模拟业务事务已提交但进程在发信前被 KILL ──
        var vehicleId = Guid.NewGuid();
        var rego = TestDataFactory.CreateRegoObject("SVC");
        var partnerEmail = TestDataFactory.CreateEmailAddress("maint_partner");

        using (var setupDb = _fixture.CreateDbContext())
        {
            var vehicle = new Vehicle(
                vehicleId,
                rego,
                "Toyota",
                "HiAce",
                2022,
                "VIN1234567890",
                new Kilometres(50000m),
                new Kilometres(10000m));

            var partner = new PartnerContact(
                Guid.NewGuid(),
                PartnerKind.Maintenance,
                "Speedy Fleet Maintenance",
                partnerEmail,
                true);

            var payload = new
            {
                VehicleId = vehicleId,
                ServiceCycleNo = 5,
                CurrentOdometerKm = 50000m,
                ThresholdKm = 50000m,
                OccurredAt = _dateTimeProvider.UtcNow
            };

            var outboxMessage = new OutboxMessage(
                Guid.NewGuid(),
                "ServiceThresholdReached",
                JsonSerializer.Serialize(payload),
                _dateTimeProvider.UtcNow);

            await setupDb.Vehicles.AddAsync(vehicle);
            await setupDb.PartnerContacts.AddAsync(partner);
            await setupDb.OutboxMessages.AddAsync(outboxMessage);
            await setupDb.SaveChangesAsync();
        }

        // ── Phase 2: 模拟服务进程重新启动并消费未处理的 Outbox 消息 ──
        using var factory = new NotificationTestWebApplicationFactory(_fixture, _dateTimeProvider);
        using (var scope = factory.Services.CreateScope())
        {
            var outboxService = scope.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            await outboxService.ProcessPendingOutboxMessagesAsync();
        }

        // ── Phase 3: 断言 Mailpit REST API (8025) 收到邮件且 EmailLog 为 Sent ──
        var mailCount = await _mailpit.GetMessageCountAsync();
        mailCount.Should().BeGreaterThanOrEqualTo(1);

        var message = await _mailpit.FindMessageBySubjectAsync(rego.Value);
        message.Should().NotBeNull();
        message!.To.Should().Contain(t => t.Address.Equals(partnerEmail.Value, StringComparison.OrdinalIgnoreCase));

        using (var verifyDb = _fixture.CreateDbContext())
        {
            var correlationId = $"CORR-SVC-{rego.Value}-CYCLE05";
            var log = await verifyDb.EmailLogs.FirstOrDefaultAsync(el => el.CorrelationId == correlationId && el.ToAddress == partnerEmail);
            log.Should().NotBeNull();
            log!.Status.Should().Be("Sent");
            log.Attempts.Should().Be(1);
            log.ToAddress.Value.Should().Be(partnerEmail.Value);
        }
    }

    [Fact]
    public async Task RetryQueue_FollowsExponentialBackoff_RetriesAfterDelayAndSucceeds()
    {
        // Arrange: 插入一条因网络故障失败的 EmailLog (Attempts = 1)
        var logId = Guid.NewGuid();
        var toEmail = TestDataFactory.CreateEmailAddress("retry_test");
        var correlationId = $"CORR-RETRY-{Guid.NewGuid():N}";

        using (var db = _fixture.CreateDbContext())
        {
            var failedLog = new EmailLog(
                logId,
                NotificationTemplateKeys.ServiceDueReminder,
                toEmail,
                "Service Reminder for Fleet",
                "Test",
                correlationId);

            failedLog.RecordFailure("SMTP Connection Failed");
            await db.EmailLogs.AddAsync(failedLog);
            await db.SaveChangesAsync();

            NotificationOutboxService.LastAttemptTimestamps[logId] = _dateTimeProvider.UtcNow;
        }

        using var factory = new NotificationTestWebApplicationFactory(_fixture, _dateTimeProvider);

        // Step 1: 立即执行重试队列（距上次尝试 0 秒，退避 1 分钟未到）-> 不应重试
        using (var scope1 = factory.Services.CreateScope())
        {
            var outboxService = scope1.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            var retried = await outboxService.ProcessRetryQueueAsync();
            retried.Should().Be(0);
        }

        // Step 2: 推进确定性时钟 65 秒（超过 1 分钟退避间隔）
        _dateTimeProvider.AdvanceTime(TimeSpan.FromSeconds(65));

        // Step 3: 再次执行重试队列 -> 应当触发重试并成功发至 Mailpit
        using (var scope2 = factory.Services.CreateScope())
        {
            var outboxService = scope2.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            var retried = await outboxService.ProcessRetryQueueAsync();
            retried.Should().Be(1);
        }

        // Assert: 状态转为 Sent，且 Mailpit 收到该邮件
        using (var verifyDb = _fixture.CreateDbContext())
        {
            var log = await verifyDb.EmailLogs.FirstOrDefaultAsync(el => el.Id == logId);
            log.Should().NotBeNull();
            log!.Status.Should().Be("Sent");
        }

        var mail = await _mailpit.FindMessageBySubjectAsync("Service Reminder for Fleet");
        mail.Should().NotBeNull();
    }
}
