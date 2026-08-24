using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Timesheets.Queries.GetTimesheetSummary;

/// <summary>
/// 工时汇总统计查询处理器（F6.5）。
/// 司机端与管理端走同一套聚合逻辑，确保两端数字完全一致、误差为 0。
/// </summary>
public sealed class GetTimesheetSummaryQueryHandler(
    IShiftEntryRepository shiftEntryRepository,
    ICurrentUser currentUser) : IRequestHandler<GetTimesheetSummaryQuery, Result<TimesheetSummaryDto>>
{
    public async Task<Result<TimesheetSummaryDto>> Handle(
        GetTimesheetSummaryQuery request,
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

            // IDOR 防护：司机不可查其他司机的工时汇总
            if (filter.DriverId.HasValue && filter.DriverId.Value != ownDriverId.Value)
            {
                return Error.Forbidden("forbidden", "Drivers are only permitted to view their own timesheet summary.");
            }

            filter = filter with { DriverId = ownDriverId.Value };
        }

        var summary = await shiftEntryRepository.GetSummaryAsync(filter, cancellationToken);
        return summary;
    }
}
