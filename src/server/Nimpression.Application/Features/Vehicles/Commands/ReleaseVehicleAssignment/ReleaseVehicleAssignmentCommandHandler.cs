using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;

namespace Nimpression.Application.Features.Vehicles.Commands.ReleaseVehicleAssignment;

/// <summary>
/// 释放车辆分派命令处理器。
/// </summary>
public sealed class ReleaseVehicleAssignmentCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ReleaseVehicleAssignmentCommand, Result>
{
    public async Task<Result> Handle(ReleaseVehicleAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await vehicleRepository.GetAssignmentByIdAsync(request.AssignmentId, cancellationToken);
        if (assignment is null)
        {
            return Error.NotFound("assignment_not_found", $"Assignment '{request.AssignmentId}' was not found.");
        }

        if (!assignment.IsActive)
        {
            return Error.Unprocessable("assignment_already_released", "Assignment is already released.");
        }

        var releasedAt = request.ReleasedAt ?? dateTimeProvider.UtcNow;
        if (releasedAt < assignment.AssignedAt)
        {
            return Error.Unprocessable("invalid_release_time", "Release time cannot be earlier than assignment time.");
        }

        assignment.Release(releasedAt);
        vehicleRepository.UpdateAssignment(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
