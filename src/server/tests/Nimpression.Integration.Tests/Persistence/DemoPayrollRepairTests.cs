using FluentAssertions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;
using Nimpression.Integration.Tests.Fixtures;
using Npgsql;

namespace Nimpression.Integration.Tests.Persistence;

[Collection("PostgreSqlCollection")]
public sealed class DemoPayrollRepairTests(PostgreSqlContainerFixture fixture)
{
    private static readonly DateTimeOffset RepairTime = new(2026, 9, 9, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewSeedIncludesMarkedSettlement_WithoutInventingFutureFinalisation()
    {
        var (_, drivers) = UserDriverSeeder.Generate();
        var (periods, slips) = PayrollSeeder.Generate(drivers);
        slips.Should().HaveCount(60);
        foreach (var slip in slips)
        {
            slip.Settlement.Should().NotBeNull();
            slip.Settlement!.IsDemo.Should().BeTrue();
            slip.Settlement.CalculatedAt.Should().BeOnOrBefore(SeedConstants.ReferenceNow);
            slip.Settlement.Request.GrossEarnings.Should().Be(slip.GrossPay.Amount);
            slip.Settlement.Calculation.BaseGross.Should().Be(slip.GrossPay.Amount);
            var period = periods.Single(item => item.Id == slip.PayPeriodId);
            slip.FinalisedAt.Should().Be(period.FinalisedAt);
            if (slip.FinalisedAt.HasValue) slip.FinalisedAt.Value.Should().BeOnOrBefore(SeedConstants.ReferenceNow);
        }
        var latestPeriod = periods.MaxBy(period => period.EndsOn)!;
        latestPeriod.FinalisedAt.Should().BeNull();
        latestPeriod.PaidAt.Should().BeNull();
        slips.Where(slip => slip.PayPeriodId == latestPeriod.Id).Should().HaveCount(10)
            .And.OnlyContain(slip => !slip.FinalisedAt.HasValue);
    }

    [Fact]
    public void SettlementJson_PreservesDemoMarker_AndOldJsonDoesNotInventOne()
    {
        var (_, drivers) = UserDriverSeeder.Generate();
        var (periods, slips) = LegacyPayrollFixture.Generate(drivers);
        var snapshot = DemoPayrollSettlement.Create(slips[0].GrossPay.Amount, periods[0], RepairTime);
        var json = JsonSerializer.SerializeToNode(snapshot)!;
        JsonSerializer.Deserialize<PayslipSettlementSnapshot>(json.ToJsonString())!.IsDemo.Should().BeTrue();
        json.AsObject().Remove("IsDemo");
        var historical = JsonSerializer.Deserialize<PayslipSettlementSnapshot>(json.ToJsonString())!;
        historical.IsDemo.Should().BeFalse();
        historical.Calculation.Should().Be(snapshot.Calculation);
    }

    [Fact]
    public async Task DryRunDoesNotWrite_ExplicitApplyRepairsExactHistoricalRows_RepeatIsIdempotent()
    {
        var options = IsolatedOptions();
        try
        {
            await SeedLegacyAsync(options);
            var before = await DatabaseSnapshotAsync(options);
            Dictionary<Guid, DateTimeOffset?> finalisedBefore;
            decimal oldGross;
            await using (var read = new AppDbContext(options))
            {
                var slips = await read.Payslips.AsNoTracking().ToListAsync();
                slips.Should().HaveCount(60);
                (await read.PayslipLines.CountAsync()).Should().Be(232);
                finalisedBefore = slips.ToDictionary(slip => slip.Id, slip => slip.FinalisedAt);
                finalisedBefore.Values.Should().OnlyContain(value => value.HasValue);
                oldGross = slips.Sum(slip => slip.GrossPay.Amount);
            }
            await using (var dryRun = new AppDbContext(options))
            {
                var summary = await DemoPayrollRepair.RunAsync(dryRun, RepairTime, repairLegacyMinimum: true);
                summary.Updated.Should().Be(0);
                summary.MinimumAdjusted.Should().Be(0);
                summary.CorrectableMinimum.Should().Be(4);
            }
            (await DatabaseSnapshotAsync(options)).Should().Equal(before);

            await using (var apply = new AppDbContext(options))
            {
                var summary = await DemoPayrollRepair.RunAsync(apply, RepairTime, apply: true, repairLegacyMinimum: true);
                summary.Updated.Should().Be(60);
                summary.MinimumAdjusted.Should().Be(4);
                summary.GrossBefore.Should().Be(oldGross);
                summary.GrossAfter.Should().Be(oldGross + 267.20m);
            }
            await using (var read = new AppDbContext(options))
            {
                var slips = await read.Payslips.AsNoTracking().Include(slip => slip.Lines).ToListAsync();
                slips.Should().HaveCount(60);
                slips.Should().OnlyContain(slip => slip.Settlement != null);
                slips.Should().OnlyContain(slip => slip.Settlement!.IsDemo);
                slips.Should().OnlyContain(slip => slip.FinalisedAt == finalisedBefore[slip.Id]);
                var expectedGross = new Dictionary<int, decimal> { [19] = 1844.15m, [29] = 2059.70m, [49] = 2083.65m, [59] = 2011.80m };
                foreach (var (suffix, gross) in expectedGross)
                {
                    var slip = slips.Single(item => item.Id == SeedSlipId(suffix));
                    slip.GrossPay.Amount.Should().Be(gross);
                    var topUp = slip.Lines.Single(line => line.Kind == "MinimumWageTopUp");
                    topUp.Rate.Amount.Should().Be(23.95m);
                    topUp.Amount.Amount.Should().Be(gross - Math.Max(slip.HoursBasedGross.Amount, slip.TripBasedGross.Amount));
                    topUp.Description.Should().Contain("23.95");
                    slip.Settlement!.Request.GrossEarnings.Should().Be(gross);
                }
                foreach (var slip in slips)
                {
                    var calculation = slip.Settlement!.Calculation;
                    calculation.BaseGross.Should().Be(slip.GrossPay.Amount);
                    calculation.Paye.Should().Be(calculation.IncomeTax + calculation.AccEarnersLevy);
                    calculation.NetPay.Should().Be(calculation.TaxableGross - calculation.Paye - calculation.StudentLoan - calculation.EmployeeKiwiSaver);
                }
            }
            var applied = await DatabaseSnapshotAsync(options);
            await using (var repeat = new AppDbContext(options))
            {
                var summary = await DemoPayrollRepair.RunAsync(repeat, RepairTime.AddDays(1), apply: true, repairLegacyMinimum: true);
                summary.Updated.Should().Be(0);
                summary.MinimumAdjusted.Should().Be(0);
                summary.AlreadySettled.Should().Be(60);
            }
            (await DatabaseSnapshotAsync(options)).Should().Equal(applied);
        }
        finally { await using var cleanup = new AppDbContext(options); await cleanup.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task ApplyWithoutMinimumPermission_SkipsStaleGross_AndNeverChangesModifiedOrNonSeedRows()
    {
        var options = IsolatedOptions();
        var otherId = Guid.NewGuid();
        try
        {
            await SeedLegacyAsync(options);
            await using (var setup = new AppDbContext(options))
            {
                var sample = await setup.Payslips.SingleAsync(slip => slip.Id == SeedSlipId(1));
                var otherPeriod = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14));
                setup.PayPeriods.Add(otherPeriod);
                setup.Payslips.Add(new Payslip(otherId, otherPeriod.Id, sample.DriverId,
                    sample.OrdinaryHours, sample.OvertimeHours, sample.HolidayHours, sample.HourlyRateSnapshot,
                    sample.HoursBasedGross, sample.CompletedTripCount, sample.TotalDistanceKm, sample.PerTripRateSnapshot,
                    sample.PerKmRateSnapshot, sample.TripBasedGross, sample.BasisUsed, sample.GrossPay, sample.MinimumWageTopUp, sample.CalculatedAt));
                await setup.SaveChangesAsync();
                await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PayslipLines\" SET \"Description\" = 'Manually reviewed pay line' WHERE \"PayslipId\" = {SeedSlipId(1)}");
                await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Payslips\" SET \"GrossPayAmount\" = \"GrossPayAmount\" + 10 WHERE \"Id\" = {SeedSlipId(2)}");
            }
            var protectedIds = new[] { SeedSlipId(1), SeedSlipId(2), otherId };
            var before = await ProtectedSnapshotAsync(options, protectedIds);
            await using (var apply = new AppDbContext(options))
            {
                var summary = await DemoPayrollRepair.RunAsync(apply, RepairTime, apply: true);
                summary.Updated.Should().Be(54);
                summary.MinimumAdjusted.Should().Be(0);
                summary.SkippedModified.Should().Be(2);
                summary.StaleGross.Should().Be(4);
            }
            (await ProtectedSnapshotAsync(options, protectedIds)).Should().Equal(before);
            await using var read = new AppDbContext(options);
            foreach (var suffix in new[] { 19, 29, 49, 59 })
                (await read.Payslips.SingleAsync(slip => slip.Id == SeedSlipId(suffix))).Settlement.Should().BeNull();
            (await read.Payslips.CountAsync()).Should().Be(61);
        }
        finally { await using var cleanup = new AppDbContext(options); await cleanup.Database.EnsureDeletedAsync(); }
    }

    private DbContextOptions<AppDbContext> IsolatedOptions()
    {
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = $"payroll_repair_{Guid.NewGuid():N}" };
        return new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
    }

    private static Guid SeedSlipId(int suffix) => new($"14000000-0000-0000-0000-{suffix:D12}");

    private static async Task SeedLegacyAsync(DbContextOptions<AppDbContext> options)
    {
        await using var setup = new AppDbContext(options);
        await setup.Database.MigrateAsync();
        var (users, drivers) = UserDriverSeeder.Generate();
        var (periods, slips) = LegacyPayrollFixture.Generate(drivers);
        setup.Users.AddRange(users);
        setup.Drivers.AddRange(drivers);
        setup.PayPeriods.AddRange(periods);
        setup.Payslips.AddRange(slips);
        await setup.SaveChangesAsync();
    }

    private static async Task<List<string>> DatabaseSnapshotAsync(DbContextOptions<AppDbContext> options)
    {
        await using var context = new AppDbContext(options);
        var slips = await context.Database.SqlQueryRaw<string>("SELECT row_to_json(p)::text AS \"Value\" FROM \"Payslips\" p ORDER BY p.\"Id\"").ToListAsync();
        var lines = await context.Database.SqlQueryRaw<string>("SELECT row_to_json(l)::text AS \"Value\" FROM \"PayslipLines\" l ORDER BY l.\"Id\"").ToListAsync();
        var periods = await context.Database.SqlQueryRaw<string>("SELECT row_to_json(p)::text AS \"Value\" FROM \"PayPeriods\" p ORDER BY p.\"Id\"").ToListAsync();
        return [.. slips, .. lines, .. periods];
    }

    private static async Task<List<string>> ProtectedSnapshotAsync(DbContextOptions<AppDbContext> options, Guid[] ids)
    {
        await using var context = new AppDbContext(options);
        var slips = await context.Database.SqlQuery<string>($"SELECT row_to_json(p)::text AS \"Value\" FROM \"Payslips\" p WHERE p.\"Id\" = ANY({ids}) ORDER BY p.\"Id\"").ToListAsync();
        var lines = await context.Database.SqlQuery<string>($"SELECT row_to_json(l)::text AS \"Value\" FROM \"PayslipLines\" l WHERE l.\"PayslipId\" = ANY({ids}) ORDER BY l.\"Id\"").ToListAsync();
        return [.. slips, .. lines];
    }
}
