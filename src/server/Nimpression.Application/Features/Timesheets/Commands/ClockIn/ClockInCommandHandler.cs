using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Timesheets.Commands.ClockIn;

/// <summary>
/// 上班打卡命令处理器（F6.1）。
/// 防重复打卡返回 409，支持 GPS 降级（正常路径，标记无坐标）。
/// </summary>
public sealed class ClockInCommandHandler(
    IShiftEntryRepository shiftEntryRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<ClockInCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ClockInCommand request, CancellationToken cancellationToken)
    {
        Guid targetDriverId;

        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var driverId = await shiftEntryRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!driverId.HasValue)
            {
                return Error.NotFound("driver_not_found", "Driver profile for the current user was not found.");
            }

            if (request.DriverId.HasValue && request.DriverId.Value != driverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers can only clock in for themselves.");
            }

            targetDriverId = driverId.Value;
        }
        else if (currentUser.Role is UserRole.Admin or UserRole.Dispatcher)
        {
            if (!request.DriverId.HasValue || request.DriverId.Value == Guid.Empty)
            {
                return Error.Validation("driver_id_required", "DriverId is required for management clock-in.");
            }

            if (!await shiftEntryRepository.DriverExistsAsync(request.DriverId.Value, cancellationToken))
            {
                return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId.Value}' was not found.");
            }

            targetDriverId = request.DriverId.Value;
        }
        else
        {
            return Error.Unauthorized("unauthorized", "User is not authorized to clock in.");
        }

        // F6.1: 未下班时再次上班打卡返回 409 Conflict
        if (await shiftEntryRepository.HasActiveShiftAsync(targetDriverId, cancellationToken))
        {
            return Error.Conflict(
                "active_shift_conflict",
                "Driver already has an active shift. Please clock out of the ongoing shift before clocking in again.");
        }

        // F6.1 GPS 降级：拒绝授权是正常业务路径，降级为无坐标
        decimal? lat = null;
        decimal? lng = null;
        if (!request.LocationUnavailable && request.Latitude.HasValue && request.Longitude.HasValue)
        {
            lat = request.Latitude.Value;
            lng = request.Longitude.Value;
        }

        var clockInAt = request.ClockInAt ?? dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        var shiftId = Guid.NewGuid();
        request.CreatedId = shiftId;

        var shift = new ShiftEntry(
            shiftId,
            targetDriverId,
            clockInAt,
            lat,
            lng,
            request.VehicleId);

        await shiftEntryRepository.AddAsync(shift, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return shiftId;
    }
}
