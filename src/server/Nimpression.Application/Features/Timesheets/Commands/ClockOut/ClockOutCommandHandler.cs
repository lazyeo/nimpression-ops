using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Timesheets.Commands.ClockOut;

/// <summary>
/// 下班打卡命令处理器（F6.1）。
/// 支持按 ShiftId 或当前司机的活跃班次下班打卡，校验时间单调性与状态。
/// </summary>
public sealed class ClockOutCommandHandler(
    IShiftEntryRepository shiftEntryRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<ClockOutCommand, Result>
{
    public async Task<Result> Handle(ClockOutCommand request, CancellationToken cancellationToken)
    {
        ShiftEntry? shift;

        if (request.ShiftId.HasValue && request.ShiftId.Value != Guid.Empty)
        {
            shift = await shiftEntryRepository.GetByIdAsync(request.ShiftId.Value, cancellationToken);
        }
        else
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
                    return Error.NotFound("driver_not_found", "Driver profile for current user was not found.");
                }

                if (request.DriverId.HasValue && request.DriverId.Value != driverId.Value)
                {
                    return Error.Forbidden("forbidden", "Drivers can only clock out of their own shifts.");
                }

                targetDriverId = driverId.Value;
            }
            else if (currentUser.Role is UserRole.Admin or UserRole.Dispatcher)
            {
                if (!request.DriverId.HasValue || request.DriverId.Value == Guid.Empty)
                {
                    return Error.Validation("driver_or_shift_required", "Either ShiftId or DriverId is required.");
                }

                targetDriverId = request.DriverId.Value;
            }
            else
            {
                return Error.Unauthorized("unauthorized", "User is not authorized to clock out.");
            }

            shift = await shiftEntryRepository.GetActiveShiftByDriverIdAsync(targetDriverId, cancellationToken);
        }

        if (shift is null)
        {
            return Error.NotFound("active_shift_not_found", "No active shift was found to clock out.");
        }

        request.TargetShiftId = shift.Id;

        // IDOR 防护：司机只能为自己的班次打卡下班
        if (currentUser.Role == UserRole.Driver)
        {
            if (currentUser.UserId.HasValue)
            {
                var ownDriverId = await shiftEntryRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
                if (ownDriverId == null || shift.DriverId != ownDriverId.Value)
                {
                    return Error.Forbidden("forbidden", "Drivers can only clock out of their own shifts.");
                }
            }
        }

        if (shift.Status != ShiftStatus.Active)
        {
            return Error.Unprocessable("shift_not_active", $"Cannot clock out of a shift in '{shift.Status}' status.");
        }

        if (request.BreakMinutes < 0)
        {
            return Error.Unprocessable("invalid_break_minutes", "Break minutes cannot be negative.");
        }

        var clockOutAt = request.ClockOutAt ?? dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        if (clockOutAt < shift.ClockInAt)
        {
            return Error.Unprocessable(
                "clock_out_before_clock_in",
                $"Clock out time ({clockOutAt}) cannot be earlier than clock in time ({shift.ClockInAt}).");
        }

        decimal? lat = null;
        decimal? lng = null;
        if (!request.LocationUnavailable && request.Latitude.HasValue && request.Longitude.HasValue)
        {
            lat = request.Latitude.Value;
            lng = request.Longitude.Value;
        }

        shift.ClockOut(clockOutAt, lat, lng, request.BreakMinutes, request.Note);
        shiftEntryRepository.Update(shift);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
