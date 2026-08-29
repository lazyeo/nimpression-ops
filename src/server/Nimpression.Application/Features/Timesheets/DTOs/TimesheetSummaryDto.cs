namespace Nimpression.Application.Features.Timesheets.DTOs;

/// <summary>
/// 工时汇总统计 DTO（F6.5 核心返回对象，司机端与管理端统一结构与口径）。
/// </summary>
public sealed record TimesheetSummaryDto(
    Guid? DriverId,
    string? DriverName,
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalShifts,
    decimal TotalPayableHours,
    decimal TotalOrdinaryHours,
    decimal TotalOvertimeHours,
    int TotalBreakMinutes,
    IReadOnlyList<TimesheetDailySummaryDto> DailySummaries);
