using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;

namespace Nimpression.Application.Features.Vehicles.Commands.UpdateVehicle;

/// <summary>
/// 更新车辆命令处理器。
/// </summary>
public sealed class UpdateVehicleCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateVehicleCommand, Result>
{
    public async Task<Result> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle '{request.Id}' was not found.");
        }

        vehicle.UpdateComplianceDates(request.WofExpiry, request.CofExpiry, request.InsuranceExpiry);
        vehicle.SetStatus(request.Status);

        vehicleRepository.UpdateVehicle(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
