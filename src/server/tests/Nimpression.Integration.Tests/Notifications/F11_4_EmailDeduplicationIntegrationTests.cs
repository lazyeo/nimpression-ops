using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
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
using Nimpression.Infrastructure.Notifications.Outbox;
using Nimpression.Infrastructure.Notifications.Smtp;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Realtime.BackgroundServices;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class F11_4_EmailDeduplicationIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainerFixture _fixture;
    private MailpitTestClient _mailpit = null!;
    private readonly List<Guid> _backlogMessageIds = [];
    private readonly TestDateTimeProvider _dateTimeProvider = TestDateTimeProvider.FromNzDate(2026, 8, 30);

    public F11_4_EmailDeduplicationIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _mailpit = _fixture.CreateMailpitClient();

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

    public async Task DisposeAsync()
    {
        try
        {
            if (_backlogMessageIds.Count > 0)
            {
                await using var db = _fixture.CreateDbContext();
                await db.OutboxMessages
                    .Where(m => _backlogMessageIds.Contains(m.Id))
                    .ExecuteDeleteAsync();
            }
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        _mailpit?.Dispose();
    }

    private WebApplicationFactory<Program> CreateFactory(DeliveryGate? gate = null) =>
        new NotificationTestWebApplicationFactory(_fixture, _dateTimeProvider)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                // These tests drive notification processing explicitly; background workers would race
                // to update the same OutboxMessage.ProcessedAt before the assertions run.
                var consumers = services.Where(descriptor =>
                    descriptor.ImplementationType == typeof(NotificationOutboxProcessorBackgroundService) ||
                    descriptor.ImplementationType == typeof(OutboxProcessorBackgroundService)).ToList();
                foreach (var consumer in consumers)
                {
                    services.Remove(consumer);
                }
                if (gate is not null)
                {
                    services.AddScoped<IEmailSender>(provider => new GatedEmailSender(
                        ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider), gate));
                }
            }));

    [Fact]
    public async Task DuplicateTriggers_WithSameCorrelationId_ProducesExactlyOneSentRecord()
    {
        // ── Step 1: 准备测试实体（司机、车辆、罚单、保险伙伴） ──
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var fineId = Guid.NewGuid();
        var outboxMessageId = Guid.NewGuid();
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

            var outboxMsg = new OutboxMessage(outboxMessageId, "FineAccepted", JsonSerializer.Serialize(payload), _dateTimeProvider.UtcNow);

            await db.Users.AddAsync(reviewer);
            await db.Users.AddAsync(user);
            await db.Drivers.AddAsync(driver);
            await db.Vehicles.AddAsync(vehicle);
            await db.Fines.AddAsync(fine);
            await db.PartnerContacts.AddAsync(partner);
            await db.OutboxMessages.AddAsync(outboxMsg);
            await db.SaveChangesAsync();
        }

        using var factory = CreateFactory();

        // ── Step 2: 第一次处理 Outbox 消息 ──
        using (var scope1 = factory.Services.CreateScope())
        {
            var outboxService = scope1.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            await outboxService.ProcessOutboxMessageAsync(outboxMessageId);
        }

        // ── Step 3: 人为重复触发第二次、第三次 ──
        using (var scope2 = factory.Services.CreateScope())
        {
            var outboxService = scope2.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            await outboxService.ProcessOutboxMessageAsync(outboxMessageId);
            await outboxService.ProcessOutboxMessageAsync(outboxMessageId);
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

    [Theory]
    [InlineData(0, false)]
    [InlineData(50, false)]
    [InlineData(0, true)]
    public async Task ConcurrentTriggers_WithSameCorrelationId_ViaTaskWhenAll_ProducesExactlyOneSentRecordAndOneEmail(int unrelatedPendingCount, bool recoverPending)
    {
        // ── Step 1: 准备测试数据 ──
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var fineId = Guid.NewGuid();
        var outboxMessageId = Guid.NewGuid();
        var reviewerUserId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var fineRef = $"INF-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var insurerEmail = TestDataFactory.CreateEmailAddress("concurrent_insurer");

        using (var db = _fixture.CreateDbContext())
        {
            var reviewer = new User(reviewerUserId, TestDataFactory.CreateEmailAddress("reviewer_c"), "Hash123", UserRole.Admin, "Reviewer Admin");
            var user = new User(driverUserId, TestDataFactory.CreateEmailAddress("driver_uc"), "Hash123456", UserRole.Driver, "Test Driver User");
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
                TestDataFactory.CreateRegoObject("CON"),
                "Toyota",
                "Corolla",
                2021,
                "VIN-CONCUR-001",
                new Kilometres(30000m),
                new Kilometres(10000m));

            var fine = new Fine(fineId, driverId, vehicleId, DateOnly.FromDateTime(DateTime.Today), "NZ Police", fineRef, new Money(150m, "NZD"), "Speeding", null);
            fine.StartReview(reviewer.Id, _dateTimeProvider.UtcNow);
            fine.Accept(reviewer.Id, _dateTimeProvider.UtcNow);

            var partner = new PartnerContact(Guid.NewGuid(), PartnerKind.Insurer, "State Insurance NZ Concurrent", insurerEmail, true);

            var payload = new
            {
                FineId = fineId,
                DriverId = driverId,
                VehicleId = vehicleId,
                Amount = new { Value = 150m, Currency = "NZD" },
                OccurredAt = _dateTimeProvider.UtcNow
            };

            var outboxMsg = new OutboxMessage(outboxMessageId, "FineAccepted", JsonSerializer.Serialize(payload), _dateTimeProvider.UtcNow);

            await db.Users.AddAsync(reviewer);
            await db.Users.AddAsync(user);
            await db.Drivers.AddAsync(driver);
            await db.Vehicles.AddAsync(vehicle);
            await db.Fines.AddAsync(fine);
            await db.PartnerContacts.AddAsync(partner);
            // Earlier unhandled events from the shared fixture must not starve this test's event.
            for (var i = 0; i < unrelatedPendingCount; i++)
            {
                var backlogId = Guid.NewGuid();
                _backlogMessageIds.Add(backlogId);
                await db.OutboxMessages.AddAsync(new OutboxMessage(
                    backlogId, "FineAccepted", "{}", _dateTimeProvider.UtcNow.AddDays(-1)));
            }

            await db.OutboxMessages.AddAsync(outboxMsg);
            if (recoverPending)
            {
                await db.EmailLogs.AddAsync(new EmailLog(
                    Guid.NewGuid(), NotificationTemplateKeys.FineAcceptedNotice, insurerEmail,
                    fineRef, "Test", $"CORR-FINE-{fineRef}"));
            }
            await db.SaveChangesAsync();
        }

        var gate = new DeliveryGate(insurerEmail.Value);
        using var factory = CreateFactory(gate);

        // ── Step 2: 两个独立 Scope / DbContext 并发执行 Task.WhenAll ──
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();

        var outboxService1 = scope1.ServiceProvider.GetRequiredService<INotificationOutboxService>();
        var outboxService2 = scope2.ServiceProvider.GetRequiredService<INotificationOutboxService>();

        if (recoverPending)
        {
            // Reused scopes may retain Pending entities while another worker completes delivery.
            foreach (var scope in new[] { scope1, scope2 })
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.OutboxMessages.SingleAsync(m => m.Id == outboxMessageId);
                await db.EmailLogs.SingleAsync(log => log.CorrelationId == $"CORR-FINE-{fineRef}" && log.ToAddress == insurerEmail);
            }
        }

        var task1 = outboxService1.ProcessOutboxMessageAsync(outboxMessageId);
        Task<bool>? task2 = null;
        try
        {
            await gate.FirstDeliveryEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            task2 = outboxService2.ProcessOutboxMessageAsync(outboxMessageId);
        }
        finally
        {
            gate.Release.TrySetResult();
        }
        await Task.WhenAll(task1, task2!);

        // ── Step 3: 断言数据库中该 CorrelationId + ToAddress 恰好只有 1 条 Sent 记录 ──
        var correlationId = $"CORR-FINE-{fineRef}";
        using (var verifyDb = _fixture.CreateDbContext())
        {
            var logs = await verifyDb.EmailLogs
                .Where(el => el.CorrelationId == correlationId && el.ToAddress == insurerEmail)
                .ToListAsync();

            logs.Should().HaveCount(1, "并发执行下数据库投递锁与唯一索引确保恰好只有 1 条日志");
            logs[0].Status.Should().Be("Sent");
            logs[0].Attempts.Should().Be(1);
        }

        // ── Step 4: 断言 Mailpit 实际只收到 1 封邮件 ──
        var allMessages = await _mailpit.GetAllMessagesAsync();
        var targetMessages = allMessages.Where(m =>
            m.To.Any(t => t.Address.Equals(insurerEmail.Value, StringComparison.OrdinalIgnoreCase)) &&
            m.Subject.Contains(fineRef, StringComparison.OrdinalIgnoreCase)).ToList();

        targetMessages.Should().HaveCount(1, "真并发下 Mailpit 实际收到的邮件必须恰好为 1 封，绝无重复投递");
    }

    [Fact]
    public async Task PartialDelivery_PreservesFailureBackoff_AndConcurrentRetriesSendOnce()
    {
        var fineId = Guid.NewGuid();
        var fineRef = fineId.ToString("N")[..8].ToUpperInvariant();
        var correlationId = $"CORR-FINE-{fineRef}";
        var firstRecipient = TestDataFactory.CreateEmailAddress("partial_success");
        var retryRecipient = TestDataFactory.CreateEmailAddress("partial_retry");
        var outboxId = Guid.NewGuid();
        _backlogMessageIds.Add(outboxId);
        await using (var db = _fixture.CreateDbContext())
        {
            db.PartnerContacts.AddRange(
                new PartnerContact(Guid.NewGuid(), PartnerKind.Insurer, "First recipient", firstRecipient, true),
                new PartnerContact(Guid.NewGuid(), PartnerKind.Insurer, "Retry recipient", retryRecipient, true));
            db.OutboxMessages.Add(new OutboxMessage(
                outboxId, "FineAccepted", JsonSerializer.Serialize(new { FineId = fineId }), _dateTimeProvider.UtcNow));
            await db.SaveChangesAsync();
        }

        var gate = new DeliveryGate(retryRecipient.Value) { FailuresRemaining = 1 };
        using var factory = CreateFactory(gate);
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<INotificationOutboxService>();
            (await service.ProcessOutboxMessageAsync(outboxId)).Should().BeTrue("both delivery outcomes were persisted and the retry queue owns the failure");
            (await service.ProcessOutboxMessageAsync(outboxId)).Should().BeFalse("outbox scans must not bypass retry backoff");
            (await service.ProcessRetryQueueAsync()).Should().Be(0, "the first backoff interval has not elapsed");
        }
        await using (var db = _fixture.CreateDbContext())
        {
            var failed = await db.EmailLogs.SingleAsync(log => log.CorrelationId == correlationId && log.ToAddress == retryRecipient);
            failed.Status.Should().Be("Failed");
            failed.Attempts.Should().Be(1);
            failed.LastError.Should().Be("Synthetic SMTP failure");
        }

        _dateTimeProvider.AdvanceTime(TimeSpan.FromSeconds(65));
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        foreach (var scope in new[] { scope1, scope2 })
        {
            // Each worker can have observed Failed before the other worker commits Sent.
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailLogs
                .SingleAsync(log => log.CorrelationId == correlationId && log.ToAddress == retryRecipient);
        }
        var service1 = scope1.ServiceProvider.GetRequiredService<INotificationOutboxService>();
        var service2 = scope2.ServiceProvider.GetRequiredService<INotificationOutboxService>();
        var retry1 = service1.ProcessRetryQueueAsync();
        Task<int>? retry2 = null;
        try
        {
            await gate.FirstDeliveryEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            retry2 = service2.ProcessRetryQueueAsync();
        }
        finally
        {
            gate.Release.TrySetResult();
        }
        (await Task.WhenAll(retry1, retry2!)).Sum().Should().Be(1);
        (await service1.ProcessOutboxMessageAsync(outboxId)).Should().BeFalse("the event was already handed off to durable delivery logs");

        await using (var db = _fixture.CreateDbContext())
        {
            var logs = await db.EmailLogs.Where(log => log.CorrelationId == correlationId &&
                (log.ToAddress == firstRecipient || log.ToAddress == retryRecipient)).ToListAsync();
            logs.Should().HaveCount(2);
            logs.Should().OnlyContain(log => log.Status == "Sent");
            logs.Single(log => log.ToAddress == firstRecipient).Attempts.Should().Be(1);
            logs.Single(log => log.ToAddress == retryRecipient).Attempts.Should().Be(2);
        }
        var messages = await _mailpit.GetAllMessagesAsync();
        foreach (var recipient in new[] { firstRecipient, retryRecipient })
        {
            messages.Count(message => message.Subject.Contains(fineRef, StringComparison.OrdinalIgnoreCase) &&
                message.To.Any(to => to.Address == recipient.Value)).Should().Be(1);
        }
    }

    private sealed class DeliveryGate(string recipient)
    {
        public string Recipient { get; } = recipient;
        public int FailuresRemaining;
        public TaskCompletionSource FirstDeliveryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class GatedEmailSender(IEmailSender inner, DeliveryGate gate) : IEmailSender
    {
        public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            if (to == gate.Recipient)
            {
                if (Interlocked.Decrement(ref gate.FailuresRemaining) >= 0)
                {
                    throw new InvalidOperationException("Synthetic SMTP failure");
                }
                gate.FirstDeliveryEntered.TrySetResult();
                await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            await inner.SendEmailAsync(to, subject, body, cancellationToken);
        }
    }

}
