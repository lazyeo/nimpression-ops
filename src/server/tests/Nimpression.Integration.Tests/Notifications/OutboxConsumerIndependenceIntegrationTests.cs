using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Notifications.Outbox;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Realtime.BackgroundServices;
using Nimpression.Infrastructure.Realtime.Services;
using Nimpression.Integration.Tests.Fixtures;
using Nimpression.Integration.Tests.Notifications.Fixtures;
using NSubstitute;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class OutboxConsumerIndependenceIntegrationTests(PostgreSqlContainerFixture fixture)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Consumers_InEitherOrder_CompleteIndependentlyWithoutRepeatingDelivery(bool realtimeFirst)
    {
        var clock = TestDateTimeProvider.FromNzDate(2026, 8, 30);
        var vehicleId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();
        var rego = TestDataFactory.CreateRegoObject("OCI");
        var recipient = TestDataFactory.CreateEmailAddress("consumer_independence");
        await using (var db = fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            db.Vehicles.Add(new Vehicle(vehicleId, rego, "Toyota", "HiAce", 2022,
                "VIN1234567890", new Kilometres(50000m), new Kilometres(10000m)));
            db.PartnerContacts.Add(new PartnerContact(Guid.NewGuid(), PartnerKind.Maintenance,
                "Consumer Independence Test", recipient, true));
            db.OutboxMessages.Add(new OutboxMessage(outboxId, "ServiceThresholdReached",
                JsonSerializer.Serialize(new { VehicleId = vehicleId, ServiceCycleNo = 5 }), clock.UtcNow));
            await db.SaveChangesAsync();
        }

        var emailSender = Substitute.For<IEmailSender>();
        var realtimeNotifier = Substitute.For<IRealtimeNotifier>();
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => fixture.CreateDbContext());
        services.AddSingleton<IDateTimeProvider>(clock);
        services.AddSingleton<IOutboxToRealtimeMapper, OutboxToRealtimeMapper>();
        services.AddSingleton(realtimeNotifier);
        await using var provider = services.BuildServiceProvider();
        using var realtime = new OutboxProcessorBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxProcessorBackgroundService>.Instance);

        async Task<bool> DeliverNotificationAsync()
        {
            await using var db = fixture.CreateDbContext();
            var service = new NotificationOutboxService(db, emailSender, clock,
                NullLogger<NotificationOutboxService>.Instance);
            return await service.ProcessOutboxMessageAsync(outboxId);
        }

        var firstHandled = realtimeFirst
            ? await realtime.ProcessMessageAsync(outboxId)
            : await DeliverNotificationAsync();
        firstHandled.Should().BeTrue();
        await using (var db = fixture.CreateDbContext())
        {
            var message = await db.OutboxMessages.SingleAsync(m => m.Id == outboxId);
            if (realtimeFirst)
            {
                message.ProcessedAt.Should().Be(clock.UtcNow);
                message.NotificationProcessedAt.Should().BeNull();
            }
            else
            {
                message.NotificationProcessedAt.Should().Be(clock.UtcNow);
                message.ProcessedAt.Should().BeNull();
            }
        }

        var secondHandled = realtimeFirst
            ? await DeliverNotificationAsync()
            : await realtime.ProcessMessageAsync(outboxId);
        secondHandled.Should().BeTrue();
        (await realtime.ProcessMessageAsync(outboxId)).Should().BeFalse();
        (await DeliverNotificationAsync()).Should().BeFalse();

        await emailSender.Received(1).SendEmailAsync(recipient.Value, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await realtimeNotifier.Received(2).PublishToGroupAsync(Arg.Any<string>(),
            Arg.Is<RealtimeMessage>(message => message.EntityId == vehicleId), Arg.Any<CancellationToken>());
        await using (var db = fixture.CreateDbContext())
        {
            var message = await db.OutboxMessages.SingleAsync(m => m.Id == outboxId);
            message.ProcessedAt.Should().Be(clock.UtcNow);
            message.NotificationProcessedAt.Should().Be(clock.UtcNow);
            var correlationId = $"CORR-SVC-{rego.Value}-CYCLE05";
            var log = await db.EmailLogs.SingleAsync(log =>
                log.CorrelationId == correlationId && log.ToAddress == recipient);
            log.Status.Should().Be("Sent");
        }
    }
}
