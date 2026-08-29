using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Services;

/// <summary>
/// 混合制薪资计算领域服务（V2 实现，严格遵守新西兰《Minimum Wage Act 1983》《Wages Protection Act 1983》）。
/// 纯领域逻辑，无外部 IO。
/// </summary>
public static class PayrollCalculatorV2
{
    public static readonly Money DefaultMinimumHourlyWage = new(23.15m, Money.DefaultCurrency);

    /// <summary>
    /// 根据司机、薪期、班次、任务与假期集合计算双口径薪资并产出工资单实体与明细行。
    /// </summary>
    public static Payslip Calculate(
        Driver driver,
        PayPeriod payPeriod,
        IEnumerable<ShiftEntry> shifts,
        IEnumerable<JobTask> tasks,
        IReadOnlySet<DateOnly>? publicHolidays = null,
        Money? minimumHourlyWage = null,
        DateTimeOffset? calculatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(payPeriod);
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(tasks);

        var holidays = publicHolidays ?? new HashSet<DateOnly>();
        var currency = driver.HourlyRate.Currency;
        var minWage = minimumHourlyWage ?? new Money(DefaultMinimumHourlyWage.Amount, currency);
        var calcAt = calculatedAt ?? DateTimeOffset.UtcNow;

        // 1. 工时口径计算
        var (ordinaryHours, overtimeHours, holidayHours, hoursBasedGross, ordinaryAmount, overtimeAmount, holidayAmount) =
            CalculateHoursGross(driver, payPeriod, shifts, holidays, currency);

        // 2. 趟次口径计算
        var (completedTripCount, totalDistanceKm, tripBasedGross, tripBaseAmount, mileageAmount) =
            CalculateTripGross(driver, payPeriod, tasks, currency);

        // 3. 结算与最低工资地板（NZ Minimum Wage Act 1983）
        var totalWorkHours = ordinaryHours + overtimeHours + holidayHours;
        var minimumWageFloor = new Money(totalWorkHours.Value * minWage.Amount, currency);

        var operationalGross = tripBasedGross > hoursBasedGross ? tripBasedGross : hoursBasedGross;
        // 相等记 Hourly
        var basisUsed = tripBasedGross > hoursBasedGross ? PayBasis.Trip : PayBasis.Hourly;
        var minimumWageTopUp = minimumWageFloor > operationalGross;
        var grossPay = minimumWageTopUp ? minimumWageFloor : operationalGross;

        // 4. 构建工资单聚合根
        var payslipId = Guid.NewGuid();
        var payslip = new Payslip(
            id: payslipId,
            payPeriodId: payPeriod.Id,
            driverId: driver.Id,
            ordinaryHours: ordinaryHours,
            overtimeHours: overtimeHours,
            holidayHours: holidayHours,
            hourlyRateSnapshot: driver.HourlyRate,
            hoursBasedGross: hoursBasedGross,
            completedTripCount: completedTripCount,
            totalDistanceKm: totalDistanceKm,
            perTripRateSnapshot: driver.PerTripRate,
            perKmRateSnapshot: driver.PerKmRate,
            tripBasedGross: tripBasedGross,
            basisUsed: basisUsed,
            grossPay: grossPay,
            minimumWageTopUp: minimumWageTopUp,
            calculatedAt: calcAt);

        // 5. 产出并保留双套口径所有明细行（F7.4）
        var lines = BuildPayslipLines(
            payslipId: payslipId,
            driver: driver,
            basisUsed: basisUsed,
            ordinaryHours: ordinaryHours,
            overtimeHours: overtimeHours,
            holidayHours: holidayHours,
            ordinaryAmount: ordinaryAmount,
            overtimeAmount: overtimeAmount,
            holidayAmount: holidayAmount,
            completedTripCount: completedTripCount,
            totalDistanceKm: totalDistanceKm,
            tripBaseAmount: tripBaseAmount,
            mileageAmount: mileageAmount,
            minimumWageTopUp: minimumWageTopUp,
            totalWorkHours: totalWorkHours,
            minWage: minWage,
            minimumWageFloor: minimumWageFloor,
            operationalGross: operationalGross);

        payslip.AddLines(lines);

        return payslip;
    }

    private static (
        WorkHours OrdinaryHours,
        WorkHours OvertimeHours,
        WorkHours HolidayHours,
        Money HoursBasedGross,
        Money OrdinaryAmount,
        Money OvertimeAmount,
        Money HolidayAmount)
    CalculateHoursGross(
        Driver driver,
        PayPeriod payPeriod,
        IEnumerable<ShiftEntry> shifts,
        IReadOnlySet<DateOnly> holidays,
        string currency)
    {
        var driverShifts = shifts
            .Where(s => s.DriverId == driver.Id && s.ClockOutAt.HasValue)
            .ToList();

        var shiftResults = new List<ShiftDurationResult>();
        foreach (var shift in driverShifts)
        {
            var result = ShiftDurationCalculator.Calculate(shift);
            if (payPeriod.Contains(result.AttributedDate))
            {
                shiftResults.Add(result);
            }
        }

        var groupedByDate = shiftResults
            .GroupBy(r => r.AttributedDate)
            .OrderBy(g => g.Key);

        decimal totalOrdinary = 0m;
        decimal totalOvertime = 0m;
        decimal totalHoliday = 0m;

        foreach (var group in groupedByDate)
        {
            var date = group.Key;
            var dailyPayableHours = group.Sum(r => r.PayableHours.Value);

            if (holidays.Contains(date))
            {
                totalHoliday += dailyPayableHours;
            }
            else
            {
                var ord = Math.Min(dailyPayableHours, 8.00m);
                var ot = Math.Max(0m, dailyPayableHours - 8.00m);
                totalOrdinary += ord;
                totalOvertime += ot;
            }
        }

        var ordinaryHours = new WorkHours(totalOrdinary);
        var overtimeHours = new WorkHours(totalOvertime);
        var holidayHours = new WorkHours(totalHoliday);

        var ordinaryRate = driver.HourlyRate;
        var overtimeRate = driver.HourlyRate * 1.5m;
        var holidayRate = driver.HourlyRate * 2.0m;

        var ordinaryAmount = new Money(ordinaryHours.Value * ordinaryRate.Amount, currency);
        var overtimeAmount = new Money(overtimeHours.Value * overtimeRate.Amount, currency);
        var holidayAmount = new Money(holidayHours.Value * holidayRate.Amount, currency);

        var hoursBasedGross = ordinaryAmount + overtimeAmount + holidayAmount;

        return (ordinaryHours, overtimeHours, holidayHours, hoursBasedGross, ordinaryAmount, overtimeAmount, holidayAmount);
    }

    private static (
        int CompletedTripCount,
        Kilometres TotalDistanceKm,
        Money TripBasedGross,
        Money TripBaseAmount,
        Money MileageAmount)
    CalculateTripGross(
        Driver driver,
        PayPeriod payPeriod,
        IEnumerable<JobTask> tasks,
        string currency)
    {
        var completedTasks = tasks
            .Where(t => t.DriverId == driver.Id &&
                        t.Status == JobTaskStatus.Completed &&
                        t.CompletedAt.HasValue &&
                        payPeriod.Contains(NzTimeZone.ToNzDateOnly(t.CompletedAt.Value)))
            .ToList();

        decimal totalDistance = 0m;

        foreach (var task in completedTasks)
        {
            if (task.EndOdometerKm.HasValue && task.StartOdometerKm.HasValue)
            {
                if (task.EndOdometerKm.Value < task.StartOdometerKm.Value)
                {
                    throw new DomainValidationException(
                        $"Task {task.Ref} end odometer ({task.EndOdometerKm.Value.Value} km) cannot be less than start odometer ({task.StartOdometerKm.Value.Value} km).");
                }

                var diff = task.EndOdometerKm.Value - task.StartOdometerKm.Value;
                if (diff.Value > 1000m)
                {
                    throw new DomainValidationException(
                        $"Task {task.Ref} odometer difference ({diff.Value} km) cannot exceed 1000 km.");
                }
            }

            var distance = task.EffectiveDistanceKm ?? Kilometres.Zero;
            totalDistance += distance.Value;
        }

        var completedTripCount = completedTasks.Count;
        var totalDistanceKm = new Kilometres(totalDistance);

        var tripBaseAmount = new Money(completedTripCount * driver.PerTripRate.Amount, currency);
        var mileageAmount = new Money(totalDistanceKm.Value * driver.PerKmRate.Amount, currency);
        var tripBasedGross = tripBaseAmount + mileageAmount;

        return (completedTripCount, totalDistanceKm, tripBasedGross, tripBaseAmount, mileageAmount);
    }

    private static List<PayslipLine> BuildPayslipLines(
        Guid payslipId,
        Driver driver,
        PayBasis basisUsed,
        WorkHours ordinaryHours,
        WorkHours overtimeHours,
        WorkHours holidayHours,
        Money ordinaryAmount,
        Money overtimeAmount,
        Money holidayAmount,
        int completedTripCount,
        Kilometres totalDistanceKm,
        Money tripBaseAmount,
        Money mileageAmount,
        bool minimumWageTopUp,
        WorkHours totalWorkHours,
        Money minWage,
        Money minimumWageFloor,
        Money operationalGross)
    {
        var lines = new List<PayslipLine>();

        // 工时明细行（始终完整产出）
        lines.Add(new PayslipLine(
            id: Guid.NewGuid(),
            payslipId: payslipId,
            basis: PayBasis.Hourly,
            kind: "OrdinaryHours",
            description: "Ordinary hours (1.0x)",
            rate: driver.HourlyRate,
            amount: ordinaryAmount,
            hours: ordinaryHours,
            distance: null,
            qty: null));

        lines.Add(new PayslipLine(
            id: Guid.NewGuid(),
            payslipId: payslipId,
            basis: PayBasis.Hourly,
            kind: "OvertimeHours",
            description: "Overtime hours (1.5x)",
            rate: driver.HourlyRate * 1.5m,
            amount: overtimeAmount,
            hours: overtimeHours,
            distance: null,
            qty: null));

        lines.Add(new PayslipLine(
            id: Guid.NewGuid(),
            payslipId: payslipId,
            basis: PayBasis.Hourly,
            kind: "HolidayHours",
            description: "Public holiday hours (2.0x)",
            rate: driver.HourlyRate * 2.0m,
            amount: holidayAmount,
            hours: holidayHours,
            distance: null,
            qty: null));

        // 趟次明细行（始终完整产出）
        lines.Add(new PayslipLine(
            id: Guid.NewGuid(),
            payslipId: payslipId,
            basis: PayBasis.Trip,
            kind: "TripBase",
            description: $"Completed trips base pay ({completedTripCount} trips)",
            rate: driver.PerTripRate,
            amount: tripBaseAmount,
            hours: null,
            distance: null,
            qty: completedTripCount));

        lines.Add(new PayslipLine(
            id: Guid.NewGuid(),
            payslipId: payslipId,
            basis: PayBasis.Trip,
            kind: "Mileage",
            description: $"Trip mileage pay ({totalDistanceKm.Value:0.##} km)",
            rate: driver.PerKmRate,
            amount: mileageAmount,
            hours: null,
            distance: totalDistanceKm,
            qty: null));

        // 最低工资补足明细行（若触发地板补足）
        if (minimumWageTopUp)
        {
            var topUpAmount = minimumWageFloor - operationalGross;
            lines.Add(new PayslipLine(
                id: Guid.NewGuid(),
                payslipId: payslipId,
                basis: basisUsed,
                kind: "MinimumWageTopUp",
                description: $"Statutory minimum wage top-up ({totalWorkHours.Value:0.##}h @ {minWage.Amount:0.00}/h, floor: {minimumWageFloor.Amount:0.00}, operational: {operationalGross.Amount:0.00})",
                rate: minWage,
                amount: topUpAmount,
                hours: totalWorkHours,
                distance: null,
                qty: null));
        }

        return lines;
    }
}
