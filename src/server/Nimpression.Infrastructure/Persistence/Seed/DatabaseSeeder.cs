using Microsoft.EntityFrameworkCore;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task<SeedSummary> SeedAsync(
        AppDbContext context,
        int randomSeed = SeedConstants.DefaultSeed,
        bool cleanExisting = false,
        CancellationToken cancellationToken = default)
    {
        if (cleanExisting)
        {
            await CleanAsync(context, cancellationToken);
        }
        else if (await context.Users.AnyAsync(cancellationToken))
        {
            // 数据已存在，直接返回现有数量统计
            return await GetSummaryAsync(context, cancellationToken);
        }

        // 1. Users & Drivers
        var (users, drivers) = UserDriverSeeder.Generate();
        await context.Users.AddRangeAsync(users, cancellationToken);
        await context.Drivers.AddRangeAsync(drivers, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // 2. Vehicles, Assignments, Readings
        var (vehicles, vehicleAssignments, odoReadings) = VehicleSeeder.Generate(drivers, users);
        await context.Vehicles.AddRangeAsync(vehicles, cancellationToken);
        await context.VehicleAssignments.AddRangeAsync(vehicleAssignments, cancellationToken);
        await context.OdometerReadings.AddRangeAsync(odoReadings, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // 3. Areas & AreaAssignments
        var (areas, areaAssignments) = AreaSeeder.Generate(drivers);
        await context.Areas.AddRangeAsync(areas, cancellationToken);
        await context.AreaAssignments.AddRangeAsync(areaAssignments, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // 4. Dispatch JobTasks
        var jobTasks = DispatchSeeder.Generate(areas, drivers, vehicles, users, randomSeed);
        await context.JobTasks.AddRangeAsync(jobTasks, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // 5. ShiftEntries
        var shifts = TimesheetSeeder.Generate(drivers, vehicles, users, randomSeed);
        await context.ShiftEntries.AddRangeAsync(shifts, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // 6. Compliance (Fines & Incidents)
        var (fines, incidents) = ComplianceSeeder.Generate(drivers, vehicles, users, randomSeed);
        await context.Fines.AddRangeAsync(fines, cancellationToken);
        await context.IncidentReports.AddRangeAsync(incidents, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // 7. Communications (Partners, Templates, News, Receipts, EmailLogs)
        var (partners, templates, news, receipts, emailLogs) = CommunicationsSeeder.Generate(users);
        await context.PartnerContacts.AddRangeAsync(partners, cancellationToken);
        await context.EmailTemplates.AddRangeAsync(templates, cancellationToken);
        await context.NewsPosts.AddRangeAsync(news, cancellationToken);
        await context.NewsReadReceipts.AddRangeAsync(receipts, cancellationToken);
        await context.EmailLogs.AddRangeAsync(emailLogs, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // 8. Payroll (PayPeriods, Payslips, Lines)
        var (payPeriods, payslips) = PayrollSeeder.Generate(drivers, randomSeed);
        await context.PayPeriods.AddRangeAsync(payPeriods, cancellationToken);
        await context.Payslips.AddRangeAsync(payslips, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // 9. Standalone (AuditEvents, DSRs, OutboxMessages)
        var (auditEvents, dsrs, outbox) = StandaloneSeeder.Generate(users, drivers);
        await context.AuditEvents.AddRangeAsync(auditEvents, cancellationToken);
        await context.DataSubjectRequests.AddRangeAsync(dsrs, cancellationToken);
        await context.OutboxMessages.AddRangeAsync(outbox, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await GetSummaryAsync(context, cancellationToken);
    }

    public static async Task CleanAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        context.PayslipLines.RemoveRange(context.PayslipLines);
        context.Payslips.RemoveRange(context.Payslips);
        context.PayPeriods.RemoveRange(context.PayPeriods);
        context.ShiftEntries.RemoveRange(context.ShiftEntries);
        context.JobTasks.RemoveRange(context.JobTasks);
        context.OdometerReadings.RemoveRange(context.OdometerReadings);
        context.VehicleAssignments.RemoveRange(context.VehicleAssignments);
        context.Vehicles.RemoveRange(context.Vehicles);
        context.AreaAssignments.RemoveRange(context.AreaAssignments);
        context.Areas.RemoveRange(context.Areas);
        context.Fines.RemoveRange(context.Fines);
        context.IncidentReports.RemoveRange(context.IncidentReports);
        context.NewsReadReceipts.RemoveRange(context.NewsReadReceipts);
        context.NewsPosts.RemoveRange(context.NewsPosts);
        context.PartnerContacts.RemoveRange(context.PartnerContacts);
        context.EmailLogs.RemoveRange(context.EmailLogs);
        context.EmailTemplates.RemoveRange(context.EmailTemplates);
        context.RefreshTokens.RemoveRange(context.RefreshTokens);
        context.Drivers.RemoveRange(context.Drivers);
        context.DataSubjectRequests.RemoveRange(context.DataSubjectRequests);
        context.OutboxMessages.RemoveRange(context.OutboxMessages);
        context.Users.RemoveRange(context.Users);

        // Note: AuditEvent is append-only by database trigger in normal operation. If cleaning in dev, delete via raw SQL disabling trigger or ignore
        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task<SeedSummary> GetSummaryAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        return new SeedSummary(
            UsersCount: await context.Users.CountAsync(cancellationToken),
            DriversCount: await context.Drivers.CountAsync(cancellationToken),
            VehiclesCount: await context.Vehicles.CountAsync(cancellationToken),
            AreasCount: await context.Areas.CountAsync(cancellationToken),
            JobTasksCount: await context.JobTasks.CountAsync(cancellationToken),
            ShiftEntriesCount: await context.ShiftEntries.CountAsync(cancellationToken),
            FinesCount: await context.Fines.CountAsync(cancellationToken),
            IncidentReportsCount: await context.IncidentReports.CountAsync(cancellationToken),
            PayPeriodsCount: await context.PayPeriods.CountAsync(cancellationToken),
            PayslipsCount: await context.Payslips.CountAsync(cancellationToken),
            NewsPostsCount: await context.NewsPosts.CountAsync(cancellationToken),
            EmailTemplatesCount: await context.EmailTemplates.CountAsync(cancellationToken));
    }
}

public record SeedSummary(
    int UsersCount,
    int DriversCount,
    int VehiclesCount,
    int AreasCount,
    int JobTasksCount,
    int ShiftEntriesCount,
    int FinesCount,
    int IncidentReportsCount,
    int PayPeriodsCount,
    int PayslipsCount,
    int NewsPostsCount,
    int EmailTemplatesCount);
