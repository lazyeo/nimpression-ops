using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetActiveVehicleAssignment;

/// <summary>
/// 获取指定车辆当前生效中分派记录查询处理器。
/// </summary>
public sealed class GetActiveVehicleAssignmentQueryHandler(
    IVehicleRepository vehicleRepository) : IRequestHandler<GetActiveVehicleAssignmentQuery, Result<VehicleAssignmentDto?>>
{
    public async Task<Result<VehicleAssignmentDto?>> Handle(GetActiveVehicleAssignmentQuery request, CancellationToken cancellationToken)
    {
        var activeAssignment = await vehicleRepository.GetActiveAssignmentByVehicleIdAsync(request.VehicleId, cancellationToken);
        if (activeAssignment is null)
        {
            return Result<VehicleAssignmentDto?>.Success(null);
        }

        var dto = new VehicleAssignmentDto(
            activeAssignment.Id,
            activeAssignment.VehicleId,
            VehicleRego: null,
            activeAssignment.DriverId,
            DriverName: null,
            DriverEmployeeNo: null,
            activeAssignment.AssignedAt,
            activeAssignment.ReleasedAt,
            activeAssignment.AssignedByUserId,
            AssignedByUserName: null,
            activeAssignment.IsActive);

        return Result<VehicleAssignmentDto?>.Success(dto);
    }
}
