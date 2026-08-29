using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Timesheets.Queries.GetShiftById;

/// <summary>
/// 按 ID 获取班次打卡记录详情处理器。
/// 包含 IDOR 越权防护（司机只能查询属于自己的班次，违者 403）。
/// </summary>
public sealed class GetShiftByIdQueryHandler(
    IShiftEntryRepository shiftEntryRepository,
    ICurrentUser currentUser) : IRequestHandler<GetShiftByIdQuery, Result<ShiftEntryDto>>
{
    public async Task<Result<ShiftEntryDto>> Handle(GetShiftByIdQuery request, CancellationToken cancellationToken)
    {
        var shift = await shiftEntryRepository.GetByIdAsync(request.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Error.NotFound("shift_not_found", $"Shift entry with ID '{request.ShiftId}' was not found.");
        }

        // IDOR 校验：司机角色只能查询自己的班次
        if (currentUser.Role == UserRole.Driver)
        {
            if (!currentUser.UserId.HasValue)
            {
                return Error.Unauthorized("unauthorized", "User is not authenticated.");
            }

            var ownDriverId = await shiftEntryRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            if (!ownDriverId.HasValue || shift.DriverId != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are only permitted to view their own shift records.");
            }
        }

        var dto = ShiftEntryDto.FromEntity(shift);
        return dto;
    }
}
