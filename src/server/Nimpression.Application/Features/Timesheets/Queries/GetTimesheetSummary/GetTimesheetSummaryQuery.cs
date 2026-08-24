using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.DTOs;

namespace Nimpression.Application.Features.Timesheets.Queries.GetTimesheetSummary;

/// <summary>
/// 工时汇总统计查询（F6.5 核心查询契约）。
/// </summary>
public sealed record GetTimesheetSummaryQuery(TimesheetSummaryFilter Filter) : IRequest<Result<TimesheetSummaryDto>>;
