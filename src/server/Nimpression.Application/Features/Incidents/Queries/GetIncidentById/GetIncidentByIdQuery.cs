using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Incidents.DTOs;

namespace Nimpression.Application.Features.Incidents.Queries.GetIncidentById;

/// <summary>
/// 按 ID 获取事故报告详情查询（F9.1 / F9.4）。
/// </summary>
public sealed record GetIncidentByIdQuery(Guid IncidentId) : IRequest<Result<IncidentReportDetailDto>>;
