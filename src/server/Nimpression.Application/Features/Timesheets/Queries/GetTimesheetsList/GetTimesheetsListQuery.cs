using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.DTOs;

namespace Nimpression.Application.Features.Timesheets.Queries.GetTimesheetsList;

/// <summary>
/// 班次打卡列表分页查询。
/// </summary>
public sealed record GetTimesheetsListQuery(TimesheetFilter Filter) : IRequest<Result<PagedResult<ShiftEntryDto>>>;
