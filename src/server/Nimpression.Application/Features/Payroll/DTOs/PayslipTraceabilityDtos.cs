using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;

namespace Nimpression.Application.Features.Payroll.DTOs;

/// <summary>
/// 班次工时追溯明细（F7.11）。
/// </summary>
public sealed record PayslipShiftDetailDto(
    Guid ShiftId,
    DateTimeOffset ClockInAt,
    DateTimeOffset? ClockOutAt,
    int BreakMinutes,
    DateOnly AttributedDate,
    decimal PayableHours)
{
    public static PayslipShiftDetailDto FromEntity(ShiftEntry shift)
    {
        var duration = shift.ClockOutAt.HasValue
            ? ShiftDurationCalculator.Calculate(shift)
            : null;

        return new PayslipShiftDetailDto(
            shift.Id,
            shift.ClockInAt,
            shift.ClockOutAt,
            shift.BreakMinutes,
            duration?.AttributedDate ?? DateOnly.FromDateTime(shift.ClockInAt.Date),
            duration?.PayableHours.Value ?? 0m);
    }
}

/// <summary>
/// 任务趟次追溯明细（F7.11）。
/// </summary>
public sealed record PayslipTripDetailDto(
    Guid JobTaskId,
    string Ref,
    string Title,
    DateTimeOffset? CompletedAt,
    decimal? EffectiveDistanceKm)
{
    public static PayslipTripDetailDto FromEntity(JobTask task) =>
        new(
            task.Id,
            task.Ref,
            task.Title,
            task.CompletedAt,
            task.EffectiveDistanceKm?.Value);
}

/// <summary>
/// 罚单独立展示项（F7.12：与工资单金额无计算关联，NZ Wages Protection Act 1983 独立分区）。
/// </summary>
public sealed record PayslipFineDto(
    Guid FineId,
    string Reference,
    DateOnly IssuedOn,
    string Authority,
    decimal Amount,
    string Currency,
    FineStatus Status,
    string Reason)
{
    public static PayslipFineDto FromEntity(Fine fine) =>
        new(
            fine.Id,
            fine.Reference,
            fine.IssuedOn,
            fine.Authority,
            fine.Amount.Amount,
            fine.Amount.Currency,
            fine.Status,
            fine.Reason);
}
