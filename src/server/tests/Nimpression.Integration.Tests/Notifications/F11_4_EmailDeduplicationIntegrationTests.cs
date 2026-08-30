using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class F11_4_EmailDeduplicationIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly MailpitTestClient _mailpit = new();
    private readonly TestDateTimeProvider _dateTimeProvider = TestDateTimeProvider.FromNzDate(2026, 8, 30);

    public F11_4_EmailDeduplicationIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();

            if (!await db.EmailTemplates.AnyAsync(t => t.Key == NotificationTemplateKeys.FineAcceptedNotice))
            {
                var template = new EmailTemplate(
                    Guid.NewGuid(),
                    NotificationTemplateKeys.FineAcceptedNotice,
                    "Infringement Notice Accepted - Ref {{FineRef}}",
                    "交通罚单责任确认通知 - 编号 {{FineRef}}",
                    "Infringement notice {{FineRef}} has been reviewed and accepted.",
                    "交通罚单 {{FineRef}} 已确认。",
                    true);

                await db.EmailTemplates.AddAsync(template);
                await db.SaveChangesAsync();
            }
        }

        await _mailpit.ClearAllMessagesAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _mailpit.Dispose();
    }

    [Fact]
    public async Task DuplicateTriggers_WithSameCorrelationId_ProducesExactlyOneSentRecord()
    {
        // ── Step 1: 准备测试实体（司机、车辆、罚单、保险伙伴） ──
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var fineId = Guid.NewGuid();
        var reviewerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var fineRef = $"INF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var insurerEmail = TestDataFactory.CreateEmailAddress("dedup_insurer");

        using (var db = _fixture.CreateDbContext())
        {
            var reviewer = new User(reviewerUserId, TestDataFactory.CreateEmailAddress("reviewer"), "Hash123", UserRole.Admin, "Reviewer Admin");
            var user = new User(driverUserId, TestDataFactory.CreateEmailAddress("driver_u"), "Hash123456", UserRole.Driver, "Test Driver User");
            var driver = new Driver(
                driverId,
                user.Id,
                TestDataFactory.CreateEmployeeNo(),
                "Class 1",
                DateOnly.FromDateTime(DateTime.Today.AddYears(2)),
                new Money(30m, "NZD"),
                new Money(15m, "NZD"),
                new Money(1.5m, "NZD"),
                "phone_enc",
                "addr_enc",
                "emg_enc",
                DateOnly.FromDateTime(DateTime.Today));

            var vehicle = new Vehicle(
                vehicleId,
                TestDataFactory.CreateRegoObject("DED"),
                "Toyota",
                "Corolla",
                2021,
                "VIN-DEDUP-001",
                new Kilometres(30000m),
                new Kilometres(10000m));

            var fine = new Fine(fineId, driverId, vehicleId, DateOnly.FromDateTime(DateTime.Today), "NZ Police", fineRef, new Money(150m, "NZD"), "Speeding", null);
            fine.StartReview(reviewer.Id, _dateTimeProvider.UtcNow);
            fine.Accept(reviewer.Id, _dateTimeProvider.UtcNow);

            var partner = new PartnerContact(Guid.NewGuid(), PartnerKind.Insurer, "State Insurance NZ", insurerEmail, true);

            var payload = new
            {
                FineId = fineId,
                DriverId = driverId,
                VehicleId = vehicleId,
                Amount = new { Value = 150m, Currency = "NZD" },
                OccurredAt = _dateTimeProvider.UtcNow
            };

            var outboxMsg = new OutboxMessage(Guid.NewGuid(), "FineAccepted", JsonSerializer.Serialize(payload), _dateTimeProvider.UtcNow);

            await db.Users.AddAsync(reviewer);
            await db.Users.AddAsync(user);
            await db.Drivers.AddAsync(driver);
            await db.Vehicles.AddAsync(vehicle);
            await db.Fines.AddAsync(fine);
            await db.PartnerContacts.AddAsync(partner);
            await db.OutboxMessages.AddAsync(outboxMsg);
            await db.SaveChangesAsync();
        }

        using var factory = new NotificationTestWebApplicationFactory(_fixture.ConnectionString, _dateTimeProvider);

        // ── Step 2: 第一次处理 Outbox 消息 ──
        using (var scope1 = factory.Services.CreateScope())
        {
            var outboxService = scope1.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            await outboxService.ProcessPendingOutboxMessagesAsync();
        }

        // ── Step 3: 人为/并发重复触发第二次、第三次 ──
        using (var scope2 = factory.Services.CreateScope())
        {
            var outboxService = scope2.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            await outboxService.ProcessPendingOutboxMessagesAsync();
            await outboxService.ProcessPendingOutboxMessagesAsync();
        }

        // ── Step 4: 断言 EmailLog 恰好只有 1 条 Sent 记录（F11.4 严格去重） ──
        var correlationId = $"CORR-FINE-{fineRef}";
        using (var verifyDb = _fixture.CreateDbContext())
        {
            var logs = await verifyDb.EmailLogs
                .Where(el => el.CorrelationId == correlationId && el.ToAddress == insurerEmail)
                .ToListAsync();

            logs.Should().HaveCount(1, "重复触发不得生成多条日志");
            logs[0].Status.Should().Be("Sent");
            logs[0].Attempts.Should().Be(1);
        }

        // ── Step 5: 断言 Mailpit 实际收件箱中该收件人恰好只收到 1 封邮件 ──
        var allMessages = await _mailpit.GetAllMessagesAsync();
        var targetMessages = allMessages.Where(m =>
            m.To.Any(t => t.Address.Equals(insurerEmail.Value, StringComparison.OrdinalIgnoreCase)) &&
            m.Subject.Contains(fineRef, StringComparison.OrdinalIgnoreCase)).ToList();

        targetMessages.Should().HaveCount(1, "Mailpit 实际接收邮件必须恰好为 1 封");
    }
}
