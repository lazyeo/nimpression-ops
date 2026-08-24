using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Timesheets.Queries.GetTimesheetsList;

/// <summary>
/// 班次打卡列表查询处理器。
/// 包含 IDOR 防护（司机角色强制限定自身 driverId，禁止翻阅他人打卡）。
/// </summary>
public sealed class GetTimesheetsListQueryHandler(
    IShiftEntryRepository shiftEntryRepository,
    ICurrentUser currentUser) : IRequestHandler<GetTimesheetsListQuery, Result<PagedResult<ShiftEntryDto>>>
{
    public async Task<Result<PagedResult<ShiftEntryDto>>> Handle(
        GetTimesheetsListQuery request,
        CancellationToken cancellationToken)
    {
        var filter = request.Filter;

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

            if (filter.DriverId.HasValue && filter.DriverId.Value != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are only permitted to query their own timesheet records.");
            }

            filter = filter with { DriverId = ownDriverId.Value };
        }

        var result = await shiftEntryRepository.GetShiftsPagedAsync(filter, cancellationToken);
        return result;
    }
}
