using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
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
    public async Task DatabaseSeeder_GeneratesCompleteDatasetScale()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();

        // Act: Run seed
        var summary = await DatabaseSeeder.SeedAsync(context, SeedConstants.DefaultSeed);

        // Assert: Verify dataset volume
        summary.UsersCount.Should().Be(13);
        summary.DriversCount.Should().Be(10);
        summary.VehiclesCount.Should().Be(11);
        summary.AreasCount.Should().Be(6);
        summary.JobTasksCount.Should().BeGreaterThan(400); // 90 days of daily dispatch runs
        summary.ShiftEntriesCount.Should().BeGreaterThan(500); // 90 days of driver timesheets
        summary.FinesCount.Should().Be(12);
        summary.IncidentReportsCount.Should().Be(6);
        summary.PayPeriodsCount.Should().Be(6); // 6 bi-weekly periods covering 84+ days
        summary.PayslipsCount.Should().Be(60); // 6 periods * 10 drivers
        summary.NewsPostsCount.Should().Be(3);
        summary.EmailTemplatesCount.Should().Be(4);
    }

    [Fact]
    public async Task DatabaseSeeder_ContainsRequiredBoundaryEdgeCases()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        await DatabaseSeeder.SeedAsync(context, SeedConstants.DefaultSeed);

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
    }

    [Fact]
    public async Task DatabaseSeeder_IsDeterministicAndRepeatable()
    {
        // Arrange: Generate two independent seed runs using same seed
        var (users1, drivers1) = UserDriverSeeder.Generate();
        var (users2, drivers2) = UserDriverSeeder.Generate();

        // Assert: Deterministic user and driver generation
        users1.Should().HaveSameCount(users2);
        for (var i = 0; i < users1.Count; i++)
        {
            users1[i].Id.Should().Be(users2[i].Id);
            users1[i].Email.Value.Should().Be(users2[i].Email.Value);
            users1[i].PasswordHash.Should().Be(users2[i].PasswordHash);
        }

        var (vehicles1, _, _) = VehicleSeeder.Generate(drivers1, users1);
        var (vehicles2, _, _) = VehicleSeeder.Generate(drivers2, users2);

        vehicles1.Should().HaveSameCount(vehicles2);
        for (var i = 0; i < vehicles1.Count; i++)
        {
            vehicles1[i].Id.Should().Be(vehicles2[i].Id);
            vehicles1[i].Rego.Value.Should().Be(vehicles2[i].Rego.Value);
            vehicles1[i].OdometerKm.Value.Should().Be(vehicles2[i].OdometerKm.Value);
        }

        var (periods1, payslips1) = PayrollSeeder.Generate(drivers1, SeedConstants.DefaultSeed);
        var (periods2, payslips2) = PayrollSeeder.Generate(drivers2, SeedConstants.DefaultSeed);

        payslips1.Should().HaveSameCount(payslips2);
        for (var i = 0; i < payslips1.Count; i++)
        {
            payslips1[i].Id.Should().Be(payslips2[i].Id);
            payslips1[i].GrossPay.Amount.Should().Be(payslips2[i].GrossPay.Amount);
            payslips1[i].MinimumWageTopUp.Should().Be(payslips2[i].MinimumWageTopUp);
        }
    }
}
