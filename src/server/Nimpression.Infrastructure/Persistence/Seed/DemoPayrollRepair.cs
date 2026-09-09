using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Seed;

public sealed record DemoPayrollRepairSummary(
    bool Applied, int Eligible, int CorrectableMinimum, int Updated, int MinimumAdjusted,
    int AlreadySettled, int SkippedModified, int Missing, int StaleGross,
    decimal GrossBefore, decimal GrossAfter, decimal Paye, decimal EmployeeKiwiSaver, decimal EmployerKiwiSaver, decimal NetPay);

/// <summary>Explicit repair of unchanged deterministic demo rows only. Dry-run is the default.
/// Finalised real records are never edited; this exception is confined to Infrastructure SQL and marked demo data.</summary>
public static class DemoPayrollRepair
{
    public static async Task<DemoPayrollRepairSummary> RunAsync(
        AppDbContext context, DateTimeOffset calculatedAt, bool apply = false, bool repairLegacyMinimum = false,
        CancellationToken cancellationToken = default)
    {
        if (context.ChangeTracker.HasChanges() || context.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Demo payroll repair requires a clean context without an active transaction.");
        var (_, expectedDrivers) = UserDriverSeeder.Generate();
        var (expectedPeriods, legacySlips) = PayrollSeeder.GenerateLegacyFingerprint(expectedDrivers);
        var (_, currentSlips) = PayrollSeeder.Generate(expectedDrivers, asOf: SeedConstants.ReferenceNow.AddDays(5));
        var currentById = currentSlips.ToDictionary(slip => slip.Id);
        var eligible = 0; var correctable = 0; var updated = 0; var minimumAdjusted = 0;
        var settled = 0; var modified = 0; var missing = 0; var stale = 0;
        decimal grossBefore = 0, grossAfter = 0, paye = 0, employeeKs = 0, employerKs = 0, net = 0;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        // Use the same parent-lock order as normal payroll mutations, then lock every compared child row.
        foreach (var expectedPeriod in expectedPeriods.OrderBy(period => period.Id))
        {
            var period = await context.PayPeriods
                .FromSqlInterpolated($"SELECT * FROM \"PayPeriods\" WHERE \"Id\" = {expectedPeriod.Id} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            foreach (var expected in legacySlips.Where(slip => slip.PayPeriodId == expectedPeriod.Id).OrderBy(slip => slip.Id))
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Payslips\" WHERE \"Id\" = {expected.Id} FOR UPDATE", cancellationToken);
                var actual = await context.Payslips.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == expected.Id, cancellationToken);
                if (actual is null) { missing++; continue; }
                if (actual.Settlement is not null) { settled++; continue; }
                if (period is null || !MatchesPeriod(period, expectedPeriod) || !actual.FinalisedAt.HasValue)
                { modified++; continue; }
                var driver = await context.Drivers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == actual.DriverId, cancellationToken);
                var expectedDriver = expectedDrivers.Single(item => item.Id == expected.DriverId);
                if (driver is null || driver.Id != expectedDriver.Id || driver.UserId != expectedDriver.UserId || driver.EmployeeNo != expectedDriver.EmployeeNo)
                { modified++; continue; }

                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"PayslipLines\" WHERE \"PayslipId\" = {actual.Id} ORDER BY \"Id\" FOR UPDATE", cancellationToken);
                var lines = await context.PayslipLines.AsNoTracking()
                    .Where(item => item.PayslipId == actual.Id).OrderBy(item => item.Id).ToListAsync(cancellationToken);
                var current = currentById[expected.Id];
                var matchesLegacy = MatchesPayslip(actual, expected, period) && MatchesLines(lines, expected.Lines);
                var matchesCurrent = MatchesPayslip(actual, current, period) && MatchesLines(lines, current.Lines);
                if (!matchesLegacy && !matchesCurrent) { modified++; continue; }

                var hours = actual.OrdinaryHours.Value + actual.OvertimeHours.Value + actual.HolidayHours.Value;
                var rate = NzAdultMinimumWage.ForPeriod(period.StartsOn, period.EndsOn).Amount;
                var minimumGross = new Money(hours * rate).Amount;
                var proposedGross = actual.GrossPay.Amount;
                PayslipLine? oldTopUp = null;
                decimal newTopUp = 0;
                string? newDescription = null;
                if (proposedGross < minimumGross)
                {
                    // Only the known old floor and its unchanged existing line can be corrected.
                    oldTopUp = lines.SingleOrDefault(line => line.Kind == "MinimumWageTopUp");
                    if (!matchesLegacy || oldTopUp is null || oldTopUp.Rate.Amount != 23.15m || !actual.MinimumWageTopUp)
                    { modified++; continue; }
                    correctable++;
                    if (!repairLegacyMinimum) { stale++; continue; }
                    proposedGross = minimumGross;
                    newTopUp = new Money(minimumGross - Math.Max(actual.HoursBasedGross.Amount, actual.TripBasedGross.Amount)).Amount;
                    newDescription = current.Lines.Single(line => line.Kind == "MinimumWageTopUp").Description;
                }
                else { eligible++; }

                var snapshot = DemoPayrollSettlement.Create(proposedGross, period, calculatedAt);
                grossBefore += actual.GrossPay.Amount; grossAfter += proposedGross;
                paye += snapshot.Calculation.Paye; employeeKs += snapshot.Calculation.EmployeeKiwiSaver;
                employerKs += snapshot.Calculation.EmployerKiwiSaverGross; net += snapshot.Calculation.NetPay;
                if (!apply) continue;

                // No domain mutation escape hatch: update precisely the inspected demo columns under locks.
                var json = JsonSerializer.Serialize(snapshot);
                var affected = await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "Payslips" SET "Settlement" = {json}::jsonb, "GrossPayAmount" = {proposedGross}
                    WHERE "Id" = {actual.Id} AND "DriverId" = {actual.DriverId} AND "PayPeriodId" = {actual.PayPeriodId}
                    AND "Settlement" IS NULL AND "FinalisedAt" = {actual.FinalisedAt}
                    AND "GrossPayAmount" = {actual.GrossPay.Amount}
                    """, cancellationToken);
                if (affected != 1) throw new InvalidOperationException("A demo payroll row changed during repair; the transaction was rolled back.");
                if (oldTopUp is not null)
                {
                    var changedLine = await context.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "PayslipLines" SET "RateAmount" = {rate}, "Amount" = {newTopUp}, "Description" = {newDescription}
                        WHERE "Id" = {oldTopUp.Id} AND "PayslipId" = {actual.Id} AND "Kind" = 'MinimumWageTopUp'
                        AND "RateAmount" = {oldTopUp.Rate.Amount} AND "Amount" = {oldTopUp.Amount.Amount}
                        """, cancellationToken);
                    if (changedLine != 1) throw new InvalidOperationException("A demo payroll line changed during repair; the transaction was rolled back.");
                    minimumAdjusted++;
                }
                updated++;
            }
        }
        if (apply) await transaction.CommitAsync(cancellationToken);
        else await transaction.RollbackAsync(cancellationToken);
        return new(apply, eligible, correctable, updated, minimumAdjusted, settled, modified, missing, stale,
            grossBefore, grossAfter, paye, employeeKs, employerKs, net);
    }

    private static bool MatchesPeriod(PayPeriod actual, PayPeriod expected) =>
        actual.Id == expected.Id && actual.StartsOn == expected.StartsOn && actual.EndsOn == expected.EndsOn &&
        actual.Status == expected.Status && actual.FinalisedAt == expected.FinalisedAt && actual.PaidAt == expected.PaidAt;

    private static bool MatchesPayslip(Payslip actual, Payslip expected, PayPeriod period) =>
        actual.Id == expected.Id && actual.DriverId == expected.DriverId && actual.PayPeriodId == expected.PayPeriodId &&
        actual.OrdinaryHours == expected.OrdinaryHours && actual.OvertimeHours == expected.OvertimeHours && actual.HolidayHours == expected.HolidayHours &&
        actual.HourlyRateSnapshot == expected.HourlyRateSnapshot && actual.HoursBasedGross == expected.HoursBasedGross &&
        actual.CompletedTripCount == expected.CompletedTripCount && actual.TotalDistanceKm == expected.TotalDistanceKm &&
        actual.PerTripRateSnapshot == expected.PerTripRateSnapshot && actual.PerKmRateSnapshot == expected.PerKmRateSnapshot &&
        actual.TripBasedGross == expected.TripBasedGross && actual.BasisUsed == expected.BasisUsed &&
        actual.GrossPay == expected.GrossPay && actual.MinimumWageTopUp == expected.MinimumWageTopUp &&
        actual.CalculatedAt == expected.CalculatedAt &&
        (actual.FinalisedAt == period.FinalisedAt || actual.FinalisedAt == expected.CalculatedAt.AddHours(1));

    private static bool MatchesLines(List<PayslipLine> actual, IReadOnlyCollection<PayslipLine> expected) =>
        actual.Count == expected.Count && actual.All(line => expected.Any(item =>
            line.Id == item.Id && line.PayslipId == item.PayslipId && line.Basis == item.Basis && line.Kind == item.Kind &&
            line.Description == item.Description && line.Hours == item.Hours && line.Distance == item.Distance && line.Qty == item.Qty &&
            line.Rate == item.Rate && line.Amount == item.Amount));
}
