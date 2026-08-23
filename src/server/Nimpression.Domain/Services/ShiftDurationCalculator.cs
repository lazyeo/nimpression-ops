using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Services;

/// <summary>
/// 班次时长计算结果。
/// </summary>
public sealed record ShiftDurationResult(
    DateOnly AttributedDate,
    TimeSpan RawDuration,
    int BreakMinutes,
    WorkHours PayableHours);

/// <summary>
/// 班次时长与归属日领域服务（纯逻辑，无 IO）。
/// 处理跨零点班次归属、新西兰 DST 夏令时切换与休息扣除。
/// </summary>
public static class ShiftDurationCalculator
{
    /// <summary>
    /// 根据班次起止时间与休息分钟数计算净工时与归属上班日。
    /// </summary>
    public static ShiftDurationResult Calculate(DateTimeOffset clockInAt, DateTimeOffset clockOutAt, int breakMinutes)
    {
        if (clockOutAt < clockInAt)
        {
            throw new DomainValidationException(
                $"Clock out time ({clockOutAt}) cannot be earlier than clock in time ({clockInAt}).");
        }

        if (breakMinutes < 0)
        {
            throw new DomainValidationException($"Break minutes cannot be negative: {breakMinutes}.");
        }

        var rawDuration = clockOutAt - clockInAt;
        var payableMinutes = Math.Max(0, (decimal)rawDuration.TotalMinutes - breakMinutes);
        var payableHours = new WorkHours(payableMinutes / 60m);
        var attributedDate = NzTimeZone.ToNzDateOnly(clockInAt);

        return new ShiftDurationResult(attributedDate, rawDuration, breakMinutes, payableHours);
    }

    /// <summary>
    /// 根据 ShiftEntry 计算工时与归属上班日。
    /// </summary>
    public static ShiftDurationResult Calculate(ShiftEntry shift)
    {
        ArgumentNullException.ThrowIfNull(shift);

        if (!shift.ClockOutAt.HasValue)
        {
            throw new DomainValidationException("Cannot calculate duration for an ongoing/uncompleted shift.");
        }

        return Calculate(shift.ClockInAt, shift.ClockOutAt.Value, shift.BreakMinutes);
    }
}
