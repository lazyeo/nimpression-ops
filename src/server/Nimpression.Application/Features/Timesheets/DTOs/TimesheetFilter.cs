using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Timesheets.DTOs;

/// <summary>
/// 班次打卡列表多维筛选条件。
/// </summary>
public sealed record TimesheetFilter(
    Guid? DriverId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    ShiftStatus? Status = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// 工时汇总筛选条件。
/// </summary>
public sealed record TimesheetSummaryFilter(
    Guid? DriverId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);
