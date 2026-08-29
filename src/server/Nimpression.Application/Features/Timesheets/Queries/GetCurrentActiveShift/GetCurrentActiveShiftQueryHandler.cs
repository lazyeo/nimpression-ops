using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Timesheets.Queries.GetCurrentActiveShift;

/// <summary>
/// 获取当前进行中活跃班次查询处理器。
/// </summary>
public sealed class GetCurrentActiveShiftQueryHandler(
    IShiftEntryRepository shiftEntryRepository,
    ICurrentUser currentUser) : IRequestHandler<GetCurrentActiveShiftQuery, Result<ShiftEntryDto?>>
{
    public async Task<Result<ShiftEntryDto?>> Handle(GetCurrentActiveShiftQuery request, CancellationToken cancellationToken)
    {
        Guid targetDriverId;

        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await shiftEntryRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue)
            {
                return Error.NotFound("driver_not_found", "Driver profile for the current user was not found.");
            }

            if (request.DriverId.HasValue && request.DriverId.Value != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are only permitted to check their own active shift.");
            }

            targetDriverId = ownDriverId.Value;
        }
        else if (currentUser.Role is UserRole.Admin or UserRole.Dispatcher)
        {
            if (!request.DriverId.HasValue || request.DriverId.Value == Guid.Empty)
            {
                return Error.Validation("driver_id_required", "DriverId is required when checking active shift as management.");
            }

            targetDriverId = request.DriverId.Value;
        }
        else
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        var shift = await shiftEntryRepository.GetActiveShiftByDriverIdAsync(targetDriverId, cancellationToken);
        if (shift is null)
        {
            return Result<ShiftEntryDto?>.Success(null);
        }

        return ShiftEntryDto.FromEntity(shift);
    }
}
