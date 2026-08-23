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
/// 计薪计算结果，包含工时与趟次两套完整核算数据及胜出结算口径。
/// </summary>
public sealed record PayrollCalculationResult(
    WorkHours OrdinaryHours,
    WorkHours OvertimeHours,
    WorkHours HolidayHours,
    Money HourlyRateSnapshot,
    Money HoursBasedGross,
    int CompletedTripCount,
    Kilometres TotalDistanceKm,
    Money PerTripRateSnapshot,
    Money PerKmRateSnapshot,
    Money TripBasedGross,
    PayBasis BasisUsed,
    Money GrossPay,
    bool MinimumWageTopUp,
    IReadOnlyList<PayslipLine> Lines)
{
    /// <summary>
    /// 根据计算结果构建已填充明细的 Payslip 实体。
    /// </summary>
    public Payslip CreatePayslip(
        Guid payslipId,
        Guid payPeriodId,
        Guid driverId,
        DateTimeOffset? calculatedAt = null)
    {
        var payslip = new Payslip(
            payslipId,
            payPeriodId,
            driverId,
            OrdinaryHours,
            OvertimeHours,
            HolidayHours,
            HourlyRateSnapshot,
            HoursBasedGross,
            CompletedTripCount,
            TotalDistanceKm,
            PerTripRateSnapshot,
            PerKmRateSnapshot,
            TripBasedGross,
            BasisUsed,
            GrossPay,
            MinimumWageTopUp,
            calculatedAt ?? DateTimeOffset.UtcNow);

        payslip.AddLines(Lines);
        return payslip;
    }
}

/// <summary>
/// 混合制薪资计算领域服务（纯逻辑，无 IO）。
/// 核心：工时保底 vs 趟次提成取高者结算，强制校验新西兰最低时薪保底，全量保留双口径明细。
/// </summary>
public static class PayrollCalculator
{
    /// <summary>
    /// 新西兰法定最低时薪默认基准（2026 年标准 $23.15/h NZD）。
    /// </summary>
    public static readonly Money DefaultNzMinimumHourlyWage = new(23.15m, Money.DefaultCurrency);

    /// <summary>
    /// 计算指定薪期内司机的薪资。
    /// </summary>
    public static PayrollCalculationResult Calculate(
        Driver driver,
        PayPeriod payPeriod,
        IEnumerable<ShiftEntry> shifts,
        IEnumerable<JobTask> tasks,
        IReadOnlySet<DateOnly>? publicHolidays = null,
        Money? minimumHourlyWage = null)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(payPeriod);
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(tasks);

        var minWage = minimumHourlyWage ?? DefaultNzMinimumHourlyWage;
        var holidays = publicHolidays ?? new HashSet<DateOnly>();
        var lines = new List<PayslipLine>();

        // ─────────────────────────────────────────────────────────────
        // 1. 工时口径核算
        // ─────────────────────────────────────────────────────────────
        var driverShifts = shifts
            .Where(s => s.DriverId == driver.Id && s.Status == ShiftStatus.Completed && s.ClockOutAt.HasValue)
            .Select(ShiftDurationCalculator.Calculate)
            .Where(r => payPeriod.Contains(r.AttributedDate))
            .ToList();

        decimal totalOrdinaryHoursValue = 0m;
        decimal totalOvertimeHoursValue = 0m;
        decimal totalHolidayHoursValue = 0m;

        var shiftsByDay = driverShifts.GroupBy(s => s.AttributedDate);
        foreach (var group in shiftsByDay)
        {
            var day = group.Key;
            var dailyHours = group.Sum(s => s.PayableHours.Value);

            if (holidays.Contains(day))
            {
                // 新西兰公众假期：当日全部工时按 2.0x 计算
                totalHolidayHoursValue += dailyHours;
            }
            else
            {
                // 普通工作日：<= 8h 1.0x，超额部分 1.5x
                var ordinary = Math.Min(8.00m, dailyHours);
                var overtime = Math.Max(0m, dailyHours - 8.00m);

                totalOrdinaryHoursValue += ordinary;
                totalOvertimeHoursValue += overtime;
            }
        }

        var ordinaryHours = new WorkHours(totalOrdinaryHoursValue);
        var overtimeHours = new WorkHours(totalOvertimeHoursValue);
        var holidayHours = new WorkHours(totalHolidayHoursValue);

        var ordinaryGross = driver.HourlyRate * ordinaryHours.Value * 1.0m;
        var overtimeGross = driver.HourlyRate * overtimeHours.Value * 1.5m;
        var holidayGross = driver.HourlyRate * holidayHours.Value * 2.0m;
        var hoursBasedGross = ordinaryGross + overtimeGross + holidayGross;

        if (ordinaryHours.Value > 0m)
        {
            lines.Add(new PayslipLine(
                Guid.NewGuid(),
                Guid.Empty,
                PayBasis.Hourly,
                "OrdinaryHours",
                $"Ordinary Hours (1.0x): {ordinaryHours.Value}h @ {driver.HourlyRate}",
                driver.HourlyRate,
                ordinaryGross,
                hours: ordinaryHours));
        }

        if (overtimeHours.Value > 0m)
        {
            lines.Add(new PayslipLine(
                Guid.NewGuid(),
                Guid.Empty,
                PayBasis.Hourly,
                "OvertimeHours",
                $"Overtime Hours (1.5x): {overtimeHours.Value}h @ {driver.HourlyRate * 1.5m}",
                driver.HourlyRate * 1.5m,
                overtimeGross,
                hours: overtimeHours));
        }

        if (holidayHours.Value > 0m)
        {
            lines.Add(new PayslipLine(
                Guid.NewGuid(),
                Guid.Empty,
                PayBasis.Hourly,
                "HolidayHours",
                $"Public Holiday Hours (2.0x): {holidayHours.Value}h @ {driver.HourlyRate * 2.0m}",
                driver.HourlyRate * 2.0m,
                holidayGross,
                hours: holidayHours));
        }

        // ─────────────────────────────────────────────────────────────
        // 2. 趟次口径核算（只算 Completed 任务）
        // ─────────────────────────────────────────────────────────────
        var completedTasks = tasks
            .Where(t => t.DriverId == driver.Id
                     && t.Status == JobTaskStatus.Completed
                     && t.CompletedAt.HasValue
                     && payPeriod.Contains(NzTimeZone.ToNzDateOnly(t.CompletedAt.Value)))
            .ToList();

        var tripCount = completedTasks.Count;
        decimal totalDistanceValue = 0m;

        foreach (var task in completedTasks)
        {
            var distance = task.EffectiveDistanceKm ?? Kilometres.Zero;
            totalDistanceValue += distance.Value;
        }

        var totalDistanceKm = new Kilometres(totalDistanceValue);
        var tripBaseGross = driver.PerTripRate * tripCount;
        var distanceBaseGross = driver.PerKmRate * totalDistanceKm.Value;
        var tripBasedGross = tripBaseGross + distanceBaseGross;

        if (tripCount > 0)
        {
            lines.Add(new PayslipLine(
                Guid.NewGuid(),
                Guid.Empty,
                PayBasis.Trip,
                "TripFee",
                $"Completed Trips: {tripCount} @ {driver.PerTripRate}",
                driver.PerTripRate,
                tripBaseGross,
                qty: tripCount));
        }

        if (totalDistanceKm.Value > 0m)
        {
            lines.Add(new PayslipLine(
                Guid.NewGuid(),
                Guid.Empty,
                PayBasis.Trip,
                "DistanceFee",
                $"Distance Travelled: {totalDistanceKm.Value}km @ {driver.PerKmRate}",
                driver.PerKmRate,
                distanceBaseGross,
                distance: totalDistanceKm));
        }

        // ─────────────────────────────────────────────────────────────
        // 3. 取高结算与最低工资保底校验
        // ─────────────────────────────────────────────────────────────
        var totalHoursValue = ordinaryHours.Value + overtimeHours.Value + holidayHours.Value;
        PayBasis basisUsed;
        Money grossPay;
        var minimumWageTopUp = false;

        // 趟次金额 > 工时金额时，趟次胜出；相等或工时高时，工时胜出
        if (tripBasedGross > hoursBasedGross)
        {
            // 校验最低工资折算保底：TripBasedGross / TotalHours >= NZ 最低时薪
            if (totalHoursValue > 0m)
            {
                var effectiveHourlyRate = tripBasedGross.Amount / totalHoursValue;
                if (effectiveHourlyRate < minWage.Amount)
                {
                    // 强制回退到工时口径并标记 MinimumWageTopUp
                    basisUsed = PayBasis.Hourly;
                    grossPay = hoursBasedGross;
                    minimumWageTopUp = true;
                }
                else
                {
                    basisUsed = PayBasis.Trip;
                    grossPay = tripBasedGross;
                }
            }
            else
            {
                basisUsed = PayBasis.Trip;
                grossPay = tripBasedGross;
            }
        }
        else
        {
            // 工时胜出或两者相等（相等时记 Hourly）
            basisUsed = PayBasis.Hourly;
            grossPay = hoursBasedGross;
        }

        return new PayrollCalculationResult(
            ordinaryHours,
            overtimeHours,
            holidayHours,
            driver.HourlyRate,
            hoursBasedGross,
            tripCount,
            totalDistanceKm,
            driver.PerTripRate,
            driver.PerKmRate,
            tripBasedGross,
            basisUsed,
            grossPay,
            minimumWageTopUp,
            lines.AsReadOnly());
    }
}
