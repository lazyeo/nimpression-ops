using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.DTOs;

namespace Nimpression.Application.Features.Drivers.Queries.GetDriversList;

/// <summary>
/// 司机列表分页查询（F2.1）。
/// 支持姓名、工号、状态、区域筛选。
/// </summary>
public sealed record GetDriversListQuery(DriverFilter Filter) : IRequest<Result<PagedResult<DriverSummaryDto>>>;
