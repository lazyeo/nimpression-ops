using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;

/// <summary>
/// 上报车辆里程读数命令处理器。
/// 
/// 关键校验：
/// 新读数必须 ≥ 该车当前最后读数（OdometerKm），否则返回 422 (UnprocessableEntity)。
/// 成功上报后：
/// 1. 持久化 OdometerReading 记录
/// 2. 更新 Vehicle.OdometerKm
/// 3. 通过 ServiceThresholdEvaluator 评估是否达到保养阈值，达到时产生领域事件并落库 Outbox
/// </summary>
public sealed class RecordOdometerReadingCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<RecordOdometerReadingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RecordOdometerReadingCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle '{request.VehicleId}' was not found.");
        }

        var driverExists = await vehicleRepository.DriverExistsAsync(request.DriverId, cancellationToken);
        if (!driverExists)
        {
            return Error.NotFound("driver_not_found", $"Driver '{request.DriverId}' was not found.");
        }

        var newKm = new Kilometres(request.ReadingKm);
        if (newKm < vehicle.OdometerKm)
        {
            return Error.Unprocessable(
                "odometer_reading_cannot_decrease",
                $"New odometer reading ({newKm.Value} km) cannot be less than current vehicle reading ({vehicle.OdometerKm.Value} km).");
        }

        var recordedAt = request.RecordedAt ?? dateTimeProvider.UtcNow;
        var reading = new OdometerReading(
            Guid.NewGuid(),
            request.VehicleId,
            request.DriverId,
            newKm,
            request.PhotoKey,
            recordedAt,
            request.Source);

        await vehicleRepository.AddOdometerReadingAsync(reading, cancellationToken);

        vehicle.UpdateOdometer(newKm);

        vehicleRepository.UpdateVehicle(vehicle);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return reading.Id;
    }
}
