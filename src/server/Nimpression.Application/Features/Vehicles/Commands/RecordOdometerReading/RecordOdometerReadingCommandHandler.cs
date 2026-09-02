using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;

/// <summary>
/// 上报车辆里程读数命令处理器。
/// 
/// 关键校验：
/// 1. 越权校验：司机只能给当前指派给自己的车辆上报里程，上报他人车辆或未指派车辆返回 403 (Forbidden)。
///    调度员与管理员可为任意有效车辆和司机记录读数。
/// 2. 新读数必须 ≥ 该车当前最后读数（OdometerKm），否则返回 422 (UnprocessableEntity)。
/// 成功上报后：
/// 1. 持久化 OdometerReading 记录
/// 2. 更新 Vehicle.OdometerKm
/// 3. 通过 ServiceThresholdEvaluator 评估是否达到保养阈值，达到时产生领域事件并落库 Outbox
/// </summary>
public sealed class RecordOdometerReadingCommandHandler(
    IVehicleRepository vehicleRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<RecordOdometerReadingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RecordOdometerReadingCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("vehicle_not_found", $"Vehicle '{request.VehicleId}' was not found.");
        }

        Guid targetDriverId;

        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await vehicleRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue)
            {
                return Error.NotFound("driver_not_found", "Driver profile was not found.");
            }

            var activeAssignment = await vehicleRepository.GetActiveAssignmentByVehicleIdAsync(request.VehicleId, cancellationToken);
            if (activeAssignment is null || activeAssignment.DriverId != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers can only record odometer readings for their currently assigned vehicle.");
            }

            if (request.DriverId != Guid.Empty && request.DriverId != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers cannot submit odometer readings for another driver.");
            }

            targetDriverId = ownDriverId.Value;
        }
        else if (currentUser.Role is UserRole.Admin or UserRole.Dispatcher)
        {
            var driverExists = await vehicleRepository.DriverExistsAsync(request.DriverId, cancellationToken);
            if (!driverExists)
            {
                return Error.NotFound("driver_not_found", $"Driver '{request.DriverId}' was not found.");
            }

            targetDriverId = request.DriverId;
        }
        else
        {
            return Error.Unauthorized("unauthorized", "User is not authorized to record odometer readings.");
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
            targetDriverId,
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
