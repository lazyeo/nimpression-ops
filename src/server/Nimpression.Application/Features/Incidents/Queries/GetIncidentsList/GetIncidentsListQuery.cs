using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Incidents.DTOs;

namespace Nimpression.Application.Features.Incidents.Queries.GetIncidentsList;

/// <summary>
/// 查询事故报告分页列表（F9.4: 可按车辆/司机/时间范围/严重度查历史）。
/// </summary>
public sealed record GetIncidentsListQuery(IncidentFilter Filter) : IRequest<Result<PagedResult<IncidentReportDto>>>;
