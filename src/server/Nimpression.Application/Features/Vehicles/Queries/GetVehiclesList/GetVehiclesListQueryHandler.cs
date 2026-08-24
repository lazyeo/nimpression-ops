using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehiclesList;

/// <summary>
/// 车辆列表分页查询处理器。
/// </summary>
public sealed class GetVehiclesListQueryHandler(
    IVehicleRepository vehicleRepository) : IRequestHandler<GetVehiclesListQuery, Result<PagedResult<VehicleSummaryDto>>>
{
    public async Task<Result<PagedResult<VehicleSummaryDto>>> Handle(GetVehiclesListQuery request, CancellationToken cancellationToken)
    {
        var filter = new VehicleFilter(
            Search: request.Search,
            Status: request.Status,
            ServiceDueOnly: request.ServiceDueOnly,
            Page: request.Page,
            PageSize: request.PageSize);

        var pagedResult = await vehicleRepository.GetVehiclesPagedAsync(filter, cancellationToken);
        return pagedResult;
    }
}
