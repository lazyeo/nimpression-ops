using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;

namespace Nimpression.Application.Features.Vehicles.Commands.UpdateVehicleStatus;

/// <summary>
/// 更新车辆状态命令处理器。
/// </summary>
public sealed class UpdateVehicleStatusCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateVehicleStatusCommand, Result>
{
    public async Task<Result> Handle(UpdateVehicleStatusCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle '{request.Id}' was not found.");
        }

        vehicle.SetStatus(request.Status);
        vehicleRepository.UpdateVehicle(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
