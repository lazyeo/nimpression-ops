using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Vehicles.Commands.RecordVehicleService;

/// <summary>
/// 记录车辆保养命令处理器。
/// </summary>
public sealed class RecordVehicleServiceCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordVehicleServiceCommand, Result>
{
    public async Task<Result> Handle(RecordVehicleServiceCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle '{request.Id}' was not found.");
        }

        var serviceKm = new Kilometres(request.ServiceOdometerKm);
        if (serviceKm < vehicle.LastServiceOdometerKm)
        {
            return Error.Unprocessable(
                "service_odometer_invalid",
                $"Service odometer ({serviceKm.Value} km) cannot be less than previous service odometer ({vehicle.LastServiceOdometerKm.Value} km).");
        }

        vehicle.RecordService(serviceKm);
        vehicleRepository.UpdateVehicle(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
