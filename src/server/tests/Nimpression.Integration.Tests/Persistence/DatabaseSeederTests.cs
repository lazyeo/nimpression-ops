using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.Persistence;

[Collection("PostgreSqlCollection")]
public class DatabaseSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;

    public DatabaseSeederTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DatabaseSeeder_ActuallyPersistsAllEntitiesToDatabase()
    {
        // Arrange
        await using (var seedContext = _fixture.CreateDbContext())
        {
            await DatabaseSeeder.SeedAsync(seedContext, SeedConstants.DefaultSeed, cleanExisting: false);
        }

        // Act: Open a fresh DbContext instance and query real PostgreSQL tables directly
        await using var verifyContext = _fixture.CreateDbContext();

        var usersCount = await verifyContext.Users.CountAsync();
        var driversCount = await verifyContext.Drivers.CountAsync();
        var vehiclesCount = await verifyContext.Vehicles.CountAsync();
        var vehicleAssignmentsCount = await verifyContext.VehicleAssignments.CountAsync();
        var odoReadingsCount = await verifyContext.OdometerReadings.CountAsync();
        var areasCount = await verifyContext.Areas.CountAsync();
        var areaAssignmentsCount = await verifyContext.AreaAssignments.CountAsync();
        var jobTasksCount = await verifyContext.JobTasks.CountAsync();
        var shiftEntriesCount = await verifyContext.ShiftEntries.CountAsync();
        var finesCount = await verifyContext.Fines.CountAsync();
        var incidentReportsCount = await verifyContext.IncidentReports.CountAsync();
        var payPeriodsCount = await verifyContext.PayPeriods.CountAsync();
        var payslipsCount = await verifyContext.Payslips.CountAsync();
        var payslipLinesCount = await verifyContext.PayslipLines.CountAsync();
        var newsPostsCount = await verifyContext.NewsPosts.CountAsync();
        var newsReadReceiptsCount = await verifyContext.NewsReadReceipts.CountAsync();
        var partnerContactsCount = await verifyContext.PartnerContacts.CountAsync();
        var emailTemplatesCount = await verifyContext.EmailTemplates.CountAsync();
        var emailLogsCount = await verifyContext.EmailLogs.CountAsync();
        var auditEventsCount = await verifyContext.AuditEvents.CountAsync();
        var dsrRequestsCount = await verifyContext.DataSubjectRequests.CountAsync();
        var outboxCount = await verifyContext.OutboxMessages.CountAsync();

        // Assert: Verify exact real row counts stored in PostgreSQL
        usersCount.Should().Be(13, "1 Admin + 2 Dispatchers + 10 Drivers");
        driversCount.Should().Be(10, "10 Active delivery drivers");
        vehiclesCount.Should().Be(11, "11 Commercial fleet trucks and utility vehicles");
        vehicleAssignmentsCount.Should().BeGreaterThanOrEqualTo(20, "History of past released and active assignments");
        odoReadingsCount.Should().BeGreaterThanOrEqualTo(10, "Odometer readings for assigned trucks");
        areasCount.Should().Be(6, "6 Auckland and regional operational areas");
        areaAssignmentsCount.Should().BeGreaterThanOrEqualTo(10, "Driver territory assignments");
        jobTasksCount.Should().BeGreaterThan(400, "90 days of operational dispatch tasks");
        shiftEntriesCount.Should().BeGreaterThan(500, "90 days of driver shift timesheets");
        finesCount.Should().Be(12, "12 Infringement fines covering all review states");
        incidentReportsCount.Should().Be(6, "6 Accident reports with insurer notifications");
        payPeriodsCount.Should().Be(6, "6 Bi-weekly pay periods covering past 90 days");
        payslipsCount.Should().Be(60, "6 Pay periods * 10 drivers");
        payslipLinesCount.Should().BeGreaterThan(180, "Line items for hours, trips, and minimum wage top-up");
        newsPostsCount.Should().Be(3, "Bilingual company announcements");
        newsReadReceiptsCount.Should().Be(12, "Driver read receipt acknowledgements");
        partnerContactsCount.Should().Be(3, "3 External partners: Insurer, Maintenance, Inspection");
        emailTemplatesCount.Should().Be(4, "4 System notification templates");
        emailLogsCount.Should().Be(3, "Delivery logs for sent notifications");
        auditEventsCount.Should().Be(5, "Append-only security audit log entries");
        dsrRequestsCount.Should().Be(1, "Data subject export request");
        outboxCount.Should().BeGreaterThanOrEqualTo(1, "Transactional outbox event messages");
    }

    [Fact]
    public async Task DatabaseSeeder_ConsecutiveRuns_AreIdempotentAndConsistent()
    {
        // Arrange & Act 1: Initial seed run
        await using (var context1 = _fixture.CreateDbContext())
        {
            await DatabaseSeeder.SeedAsync(context1, SeedConstants.DefaultSeed);
        }

        int initialUsers, initialDrivers, initialVehicles, initialTasks, initialShifts, initialPayslips;
        await using (var verifyContext1 = _fixture.CreateDbContext())
        {
            initialUsers = await verifyContext1.Users.CountAsync();
            initialDrivers = await verifyContext1.Drivers.CountAsync();
            initialVehicles = await verifyContext1.Vehicles.CountAsync();
            initialTasks = await verifyContext1.JobTasks.CountAsync();
            initialShifts = await verifyContext1.ShiftEntries.CountAsync();
            initialPayslips = await verifyContext1.Payslips.CountAsync();
        }

        // Act 2: Second consecutive seed run on existing database
        await using (var context2 = _fixture.CreateDbContext())
        {
            var summary2 = await DatabaseSeeder.SeedAsync(context2, SeedConstants.DefaultSeed);
            summary2.UsersCount.Should().Be(initialUsers);
            summary2.DriversCount.Should().Be(initialDrivers);
            summary2.VehiclesCount.Should().Be(initialVehicles);
            summary2.JobTasksCount.Should().Be(initialTasks);
            summary2.ShiftEntriesCount.Should().Be(initialShifts);
            summary2.PayslipsCount.Should().Be(initialPayslips);
        }

        // Assert: Database row counts remain completely identical and no duplicates were created
        await using (var verifyContext2 = _fixture.CreateDbContext())
        {
            (await verifyContext2.Users.CountAsync()).Should().Be(initialUsers);
            (await verifyContext2.Drivers.CountAsync()).Should().Be(initialDrivers);
            (await verifyContext2.Vehicles.CountAsync()).Should().Be(initialVehicles);
            (await verifyContext2.JobTasks.CountAsync()).Should().Be(initialTasks);
            (await verifyContext2.ShiftEntries.CountAsync()).Should().Be(initialShifts);
            (await verifyContext2.Payslips.CountAsync()).Should().Be(initialPayslips);
        }
    }

    [Fact]
    public async Task DatabaseSeeder_ContainsRequiredBoundaryEdgeCases()
    {
        // Arrange
        await using (var seedContext = _fixture.CreateDbContext())
        {
            await DatabaseSeeder.SeedAsync(seedContext, SeedConstants.DefaultSeed);
        }

        await using var context = _fixture.CreateDbContext();

        // 1. Edge Case: Vehicle reaching maintenance service threshold
        var vehicles = await context.Vehicles.ToListAsync();
        var serviceDueTruck = vehicles.FirstOrDefault(v => v.DistanceSinceLastService.Value >= v.ServiceIntervalKm.Value);
        serviceDueTruck.Should().NotBeNull();
        serviceDueTruck!.Rego.Value.Should().Be("NIM003");

        // 2. Edge Case: Sub-minimum wage driver rate triggering MinimumWageTopUp
        var subMinWageDriver = await context.Drivers.FirstOrDefaultAsync(d => d.HourlyRate.Amount < 23.15m);
        subMinWageDriver.Should().NotBeNull();
        subMinWageDriver!.HourlyRate.Amount.Should().Be(22.00m);

        var toppedUpPayslips = await context.Payslips
            .Where(p => p.MinimumWageTopUp)
            .ToListAsync();
        toppedUpPayslips.Should().NotBeEmpty();
        toppedUpPayslips.Should().Contain(p => p.DriverId == subMinWageDriver.Id);

        // 3. Edge Case: Shift crossing DST transition date
        var shifts = await context.ShiftEntries.ToListAsync();
        var dstShift = shifts.FirstOrDefault(s => s.ClockInAt.Year == 2026 && s.ClockInAt.Month == 4 && s.ClockInAt.Day == 4);
        dstShift.Should().NotBeNull();
        dstShift!.Note.Should().Contain("DST");

        // 4. Edge Case: WOF, COF and Insurance expiry distribution (Expired, 30-day window, Normal)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // WOF: expired (< today), <= 30 days, normal (> 30 days)
        vehicles.Count(v => v.WofExpiry.HasValue && v.WofExpiry.Value < today).Should().BeGreaterThanOrEqualTo(1);
        vehicles.Count(v => v.WofExpiry.HasValue && v.WofExpiry.Value >= today && v.WofExpiry.Value <= today.AddDays(30)).Should().BeGreaterThanOrEqualTo(1);
        vehicles.Count(v => v.WofExpiry.HasValue && v.WofExpiry.Value > today.AddDays(30)).Should().BeGreaterThanOrEqualTo(1);

        // COF: expired (< today), <= 30 days, normal (> 30 days)
        vehicles.Count(v => v.CofExpiry.HasValue && v.CofExpiry.Value < today).Should().BeGreaterThanOrEqualTo(1);
        vehicles.Count(v => v.CofExpiry.HasValue && v.CofExpiry.Value >= today && v.CofExpiry.Value <= today.AddDays(30)).Should().BeGreaterThanOrEqualTo(1);
        vehicles.Count(v => v.CofExpiry.HasValue && v.CofExpiry.Value > today.AddDays(30)).Should().BeGreaterThanOrEqualTo(1);

        // Insurance: expired (< today), <= 30 days, normal (> 30 days)
        vehicles.Count(v => v.InsuranceExpiry.HasValue && v.InsuranceExpiry.Value < today).Should().BeGreaterThanOrEqualTo(1);
        vehicles.Count(v => v.InsuranceExpiry.HasValue && v.InsuranceExpiry.Value >= today && v.InsuranceExpiry.Value <= today.AddDays(30)).Should().BeGreaterThanOrEqualTo(1);
        vehicles.Count(v => v.InsuranceExpiry.HasValue && v.InsuranceExpiry.Value > today.AddDays(30)).Should().BeGreaterThanOrEqualTo(1);

        // 5. Edge Case: Driver licence 30-day expiry warning
        var drivers = await context.Drivers.ToListAsync();
        drivers.Count(d => d.LicenceExpiry >= today && d.LicenceExpiry <= today.AddDays(30)).Should().BeGreaterThanOrEqualTo(1);
    }
}
