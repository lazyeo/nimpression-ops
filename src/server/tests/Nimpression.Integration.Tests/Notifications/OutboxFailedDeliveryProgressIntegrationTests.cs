using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
using NSubstitute;

namespace Nimpression.Integration.Tests.Notifications;

[Collection("PostgreSqlCollection")]
public sealed class OutboxFailedDeliveryProgressIntegrationTests(PostgreSqlContainerFixture fixture)
{
    [Fact]
    public async Task ExhaustedDeliveries_DoNotBlockTheNextOutboxBatch()
    {
        var clock = TestDateTimeProvider.FromNzDate(2026, 8, 30);
        var vehicleId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var rego = TestDataFactory.CreateRegoObject("OFP");
        var recipient = TestDataFactory.CreateEmailAddress("failed_outbox_progress");
        var messageIds = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray();
        var correlations = Enumerable.Range(1, 51)
            .Select(cycle => $"CORR-SVC-{rego.Value}-CYCLE{cycle:D2}").ToArray();
        var emailSender = Substitute.For<IEmailSender>();

        try
        {
            await using (var db = fixture.CreateDbContext())
            {
                await db.Database.MigrateAsync();
                db.Vehicles.Add(new Vehicle(vehicleId, rego, "Toyota", "HiAce", 2022,
                    "VIN1234567890", new Kilometres(50000m), new Kilometres(10000m)));
                db.PartnerContacts.Add(new PartnerContact(partnerId, PartnerKind.Maintenance,
                    "Failed Outbox Progress Test", recipient, true));

                for (var index = 0; index < messageIds.Length; index++)
                {
                    // These dates place our backlog before ordinary fixture events without modifying them.
                    var occurredAt = new DateTimeOffset(index < 50 ? 2000 : 2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    db.OutboxMessages.Add(new OutboxMessage(messageIds[index], "ServiceThresholdReached",
                        JsonSerializer.Serialize(new { VehicleId = vehicleId, ServiceCycleNo = index + 1 }), occurredAt));
                    if (index < 50)
                    {
                        var log = new EmailLog(Guid.NewGuid(), NotificationTemplateKeys.ServiceDueReminder,
                            recipient, "Exhausted notification", "Test", correlations[index]);
                        log.RecordFailure("SMTP unavailable");
                        log.RecordFailure("SMTP unavailable");
                        log.RecordFailure("SMTP unavailable");
                        db.EmailLogs.Add(log);
                    }
                }
                await db.SaveChangesAsync();
            }

            await using (var db = fixture.CreateDbContext())
            {
                var service = new NotificationOutboxService(db, emailSender, clock,
                    NullLogger<NotificationOutboxService>.Instance);
                (await service.ProcessPendingOutboxMessagesAsync()).Should().Be(50,
                    "durable failed records are already handed to the retry queue");
            }
            await using (var db = fixture.CreateDbContext())
            {
                var service = new NotificationOutboxService(db, emailSender, clock,
                    NullLogger<NotificationOutboxService>.Instance);
                await service.ProcessPendingOutboxMessagesAsync();
            }

            await emailSender.Received(1).SendEmailAsync(recipient.Value,
                Arg.Is<string>(subject => subject.Contains(rego.Value)), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await using (var db = fixture.CreateDbContext())
            {
                var messages = await db.OutboxMessages.Where(message => messageIds.Contains(message.Id)).ToListAsync();
                messages.Should().HaveCount(51).And.OnlyContain(message => message.NotificationProcessedAt != null);
                var logs = await db.EmailLogs.Where(log =>
                    correlations.Contains(log.CorrelationId) && log.ToAddress == recipient).ToListAsync();
                logs.Count(log => log.Status == "Failed" && log.Attempts == 3).Should().Be(50);
                logs.Single(log => log.CorrelationId == correlations[50]).Status.Should().Be("Sent");
            }
        }
        finally
        {
            await using var db = fixture.CreateDbContext();
            var logIds = await db.EmailLogs.Where(log => correlations.Contains(log.CorrelationId))
                .Select(log => log.Id).ToListAsync();
            await db.EmailLogs.Where(log => logIds.Contains(log.Id)).ExecuteDeleteAsync();
            await db.OutboxMessages.Where(message => messageIds.Contains(message.Id)).ExecuteDeleteAsync();
            await db.PartnerContacts.Where(partner => partner.Id == partnerId).ExecuteDeleteAsync();
            await db.Vehicles.Where(vehicle => vehicle.Id == vehicleId).ExecuteDeleteAsync();
            foreach (var logId in logIds)
            {
                NotificationOutboxService.LastAttemptTimestamps.TryRemove(logId, out _);
            }
        }
    }
}
