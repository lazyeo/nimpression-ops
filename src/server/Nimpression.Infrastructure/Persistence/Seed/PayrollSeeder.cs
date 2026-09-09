using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class PayrollSeeder
{
    public static (List<PayPeriod> PayPeriods, List<Payslip> Payslips) Generate(
        List<Driver> drivers,
        int randomSeed = SeedConstants.DefaultSeed,
        DateTimeOffset? asOf = null)
    {
        var cutoff = asOf ?? SeedConstants.ReferenceNow;
        var rng = new Random(randomSeed);
        var payPeriods = new List<PayPeriod>();
        var payslips = new List<Payslip>();

        // 生成 6 个双周薪期（覆盖过去 84 天）
        var periodCount = 6;
        var periodIdCounter = 1;
        var payslipIdCounter = 1;
        var lineIdCounter = 1;

        for (var p = periodCount; p >= 1; p--)
        {
            var startsOn = SeedConstants.ReferenceDate.AddDays(-p * 14);
            var endsOn = startsOn.AddDays(13);

            var calculatedAt = new DateTimeOffset(endsOn.Year, endsOn.Month, endsOn.Day, 17, 0, 0, TimeSpan.FromHours(12));
            if (calculatedAt > cutoff)
            {
                continue;
            }

            var statutoryMinimumWageRate = NzAdultMinimumWage.ForPeriod(startsOn, endsOn).Amount;

            var payPeriodId = new Guid($"13000000-0000-0000-0000-{periodIdCounter++:D12}");
            var payPeriod = new PayPeriod(payPeriodId, startsOn, endsOn, PayPeriodStatus.Calculating);
            var finalisedAt = calculatedAt.AddDays(1).AddHours(1);
            if (finalisedAt <= cutoff)
            {
                payPeriod.Finalise(finalisedAt);
            }

            var paidAt = calculatedAt.AddDays(3).AddHours(-7);
            if (p > 1 && paidAt <= cutoff)
            {
                payPeriod.MarkPaid(paidAt);
            }

            payPeriods.Add(payPeriod);

            // 为 10 名司机生成工资单
            for (var d = 0; d < drivers.Count; d++)
            {
                var driver = drivers[d];
                var payslipId = new Guid($"14000000-0000-0000-0000-{payslipIdCounter++:D12}");

                // 工时口径数据
                var ordHours = new WorkHours(70.0m + rng.Next(0, 15));
                var otHours = new WorkHours(rng.Next(0, 8));
                var holHours = new WorkHours(0m);
                var totalHours = ordHours.Value + otHours.Value + holHours.Value;

                var ordAmount = ordHours.Value * driver.HourlyRate.Amount;
                var otAmount = otHours.Value * driver.HourlyRate.Amount * 1.5m;
                var holAmount = holHours.Value * driver.HourlyRate.Amount * 2.0m;
                var hoursBasedGross = new Money(ordAmount + otAmount + holAmount);

                // 趟次口径数据
                var tripCount = rng.Next(15, 30);
                var totalDist = new Kilometres(rng.Next(800, 2200));
                var tripAmount = tripCount * driver.PerTripRate.Amount;
                var distAmount = totalDist.Value * driver.PerKmRate.Amount;
                var tripBasedGross = new Money(tripAmount + distAmount);

                // 三者取高与最低工资保底 (F7.5)
                var minWageFloor = totalHours * statutoryMinimumWageRate;
                var maxBasisGross = Math.Max(hoursBasedGross.Amount, tripBasedGross.Amount);
                var minWageTopUp = maxBasisGross < minWageFloor;
                var finalGrossAmount = Math.Max(maxBasisGross, minWageFloor);
                var grossPay = new Money(finalGrossAmount);

                var basisUsed = tripBasedGross.Amount > hoursBasedGross.Amount ? PayBasis.Trip : PayBasis.Hourly;

                var payslip = new Payslip(
                    payslipId,
                    payPeriod.Id,
                    driver.Id,
                    ordHours,
                    otHours,
                    holHours,
                    driver.HourlyRate,
                    hoursBasedGross,
                    tripCount,
                    totalDist,
                    driver.PerTripRate,
                    driver.PerKmRate,
                    tripBasedGross,
                    basisUsed,
                    grossPay,
                    minWageTopUp,
                    calculatedAt);

                // 添加明细行 (双口径均完整保留)
                // 1. 工时行
                payslip.AddLine(new PayslipLine(
                    new Guid($"15000000-0000-0000-0000-{lineIdCounter++:D12}"),
                    payslipId,
                    PayBasis.Hourly,
                    "OrdinaryHours",
                    $"Ordinary Hours (1.0x) - {ordHours.Value} hrs @ ${driver.HourlyRate.Amount:F2}/hr",
                    driver.HourlyRate,
                    new Money(ordAmount),
                    ordHours));

                if (otHours.Value > 0)
                {
                    payslip.AddLine(new PayslipLine(
                        new Guid($"15000000-0000-0000-0000-{lineIdCounter++:D12}"),
                        payslipId,
                        PayBasis.Hourly,
                        "OvertimeHours",
                        $"Overtime Hours (1.5x) - {otHours.Value} hrs @ ${driver.HourlyRate.Amount * 1.5m:F2}/hr",
                        driver.HourlyRate * 1.5m,
                        new Money(otAmount),
                        otHours));
                }

                // 2. 趟次行
                payslip.AddLine(new PayslipLine(
                    new Guid($"15000000-0000-0000-0000-{lineIdCounter++:D12}"),
                    payslipId,
                    PayBasis.Trip,
                    "CompletedTrips",
                    $"Completed Delivery Trips - {tripCount} trips @ ${driver.PerTripRate.Amount:F2}/trip",
                    driver.PerTripRate,
                    new Money(tripAmount),
                    qty: tripCount));

                payslip.AddLine(new PayslipLine(
                    new Guid($"15000000-0000-0000-0000-{lineIdCounter++:D12}"),
                    payslipId,
                    PayBasis.Trip,
                    "DistanceAllowance",
                    $"Trip Distance Allowance - {totalDist.Value} km @ ${driver.PerKmRate.Amount:F2}/km",
                    driver.PerKmRate,
                    new Money(distAmount),
                    distance: totalDist));

                // 3. 最低工资保底补足行 (若触发)
                if (minWageTopUp)
                {
                    var topUpDiff = minWageFloor - maxBasisGross;
                    payslip.AddLine(new PayslipLine(
                        new Guid($"15000000-0000-0000-0000-{lineIdCounter++:D12}"),
                        payslipId,
                        basisUsed,
                        "MinimumWageTopUp",
                        $"NZ Statutory Minimum Wage Top-up floor (${statutoryMinimumWageRate:0.00}/hr guard for {totalHours} total hours)",
                        new Money(statutoryMinimumWageRate),
                        new Money(topUpDiff)));
                }

                if (payPeriod.FinalisedAt.HasValue)
                {
                    payslip.Finalise(payPeriod.FinalisedAt.Value);
                }

                payslips.Add(payslip);
            }
        }

        return (payPeriods, payslips);
    }
}
