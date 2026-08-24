using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehiclesList;

/// <summary>
/// 车辆列表分页与筛选查询。
/// </summary>
public sealed record GetVehiclesListQuery(
    string? Search = null,
    VehicleStatus? Status = null,
    bool? ServiceDueOnly = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<VehicleSummaryDto>>>;
