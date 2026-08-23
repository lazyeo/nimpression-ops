using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehicleById;

/// <summary>
/// 按 ID 获取车辆详情查询处理器。
/// </summary>
public sealed class GetVehicleByIdQueryHandler(
    IVehicleRepository vehicleRepository) : IRequestHandler<GetVehicleByIdQuery, Result<VehicleDetailDto>>
{
    public async Task<Result<VehicleDetailDto>> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicleDetail = await vehicleRepository.GetVehicleDetailAsync(request.Id, cancellationToken);
        if (vehicleDetail is null)
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle '{request.Id}' was not found.");
        }

        return vehicleDetail;
    }
}
