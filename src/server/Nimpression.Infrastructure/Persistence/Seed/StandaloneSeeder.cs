using System.Text.Json;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class StandaloneSeeder
{
    public static (
        List<AuditEvent> AuditEvents,
        List<DataSubjectRequest> DsrRequests,
        List<OutboxMessage> OutboxMessages) Generate(
        List<User> users,
        List<Driver> drivers)
    {
        var adminUser = users.First(u => u.Role == UserRole.Admin);
        var auditEvents = new List<AuditEvent>();
        var dsrRequests = new List<DataSubjectRequest>();
        var outboxMessages = new List<OutboxMessage>();

        // 1. AuditEvents
        var auditConfigs = new (string Action, string EntityType, string EntityId, string? Before, string? After, DateTimeOffset Occurred)[]
        {
            ("User.Login", "User", adminUser.Id.ToString(), null, "{\"ip\":\"192.168.1.100\",\"userAgent\":\"Mozilla/5.0\"}", SeedConstants.ReferenceNow.AddDays(-80)),
            ("Driver.RateUpdated", "Driver", drivers[0].Id.ToString(), "{\"hourlyRate\":30.00}", "{\"hourlyRate\":32.50}", SeedConstants.ReferenceNow.AddDays(-60)),
            ("Shift.AdminCorrected", "ShiftEntry", "A0000000-0000-0000-0000-000000000025", "{\"clockInAt\":\"2026-07-20T08:00:00+12:00\"}", "{\"clockInAt\":\"2026-07-20T07:45:00+12:00\"}", SeedConstants.ReferenceNow.AddDays(-34)),
            ("Fine.Accepted", "Fine", "B0000000-0000-0000-0000-000000000003", "{\"status\":\"UnderReview\"}", "{\"status\":\"Accepted\"}", SeedConstants.ReferenceNow.AddDays(-20)),
            ("Payroll.Finalised", "PayPeriod", "13000000-0000-0000-0000-000000000002", "{\"status\":\"Calculating\"}", "{\"status\":\"Finalised\"}", SeedConstants.ReferenceNow.AddDays(-14))
        };

        for (var i = 0; i < auditConfigs.Length; i++)
        {
            var cfg = auditConfigs[i];
            var auditId = new Guid($"16000000-0000-0000-0000-{i + 1:D12}");
            auditEvents.Add(new AuditEvent(
                auditId,
                cfg.Action,
                cfg.EntityType,
                cfg.EntityId,
                cfg.Occurred,
                adminUser.Id,
                UserRole.Admin,
                cfg.Before,
                cfg.After,
                "192.168.1.100",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"));
        }

        // 2. DataSubjectRequests
        var dsrId = new Guid("17000000-0000-0000-0000-000000000001");
        var dsr = new DataSubjectRequest(
            dsrId,
            drivers[0].UserId,
            DataSubjectRequestKind.Export,
            SeedConstants.ReferenceNow.AddDays(-15));
        dsr.Complete("exports/user_drv001_data_20260808.zip", SeedConstants.ReferenceNow.AddDays(-14));
        dsrRequests.Add(dsr);

        // 3. OutboxMessages (已投递的历史领域事件)
        var sampleEvent = new DriverDeactivated(drivers[0].Id, drivers[0].UserId, SeedConstants.ReferenceNow.AddDays(-50));
        var outboxMsg = new OutboxMessage(
            new Guid("18000000-0000-0000-0000-000000000001"),
            nameof(DriverDeactivated),
            JsonSerializer.Serialize(sampleEvent),
            sampleEvent.OccurredAt);
        outboxMsg.MarkProcessed(sampleEvent.OccurredAt.AddSeconds(2));
        outboxMessages.Add(outboxMsg);

        return (auditEvents, dsrRequests, outboxMessages);
    }
}
