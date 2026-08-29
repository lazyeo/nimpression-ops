namespace Nimpression.Application.Features.Timesheets.DTOs;

/// <summary>
/// 按日工时聚合 DTO。
/// </summary>
public sealed record TimesheetDailySummaryDto(
    DateOnly Date,
    int ShiftCount,
    decimal PayableHours,
    decimal OrdinaryHours,
    decimal OvertimeHours,
    int BreakMinutes);
