using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehicleAssignments;

/// <summary>
/// 车辆分派历史查询处理器。
/// </summary>
public sealed class GetVehicleAssignmentsQueryHandler(
    IVehicleRepository vehicleRepository) : IRequestHandler<GetVehicleAssignmentsQuery, Result<IReadOnlyList<VehicleAssignmentDto>>>
{
    public async Task<Result<IReadOnlyList<VehicleAssignmentDto>>> Handle(GetVehicleAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await vehicleRepository.GetAssignmentsByVehicleIdAsync(request.VehicleId, cancellationToken);
        return Result<IReadOnlyList<VehicleAssignmentDto>>.Success(assignments);
    }
}
