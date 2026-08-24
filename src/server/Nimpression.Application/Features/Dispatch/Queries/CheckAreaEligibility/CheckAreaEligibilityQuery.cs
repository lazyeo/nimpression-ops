using System.Globalization;
using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;

namespace Nimpression.Application.Features.Dispatch.Queries.CheckAreaEligibility;

/// <summary>
/// 派单区域资格检查查询（F4.3）。
/// </summary>
public sealed record CheckAreaEligibilityQuery(
    Guid DriverId,
    Guid AreaId,
    DateOnly ScheduledDate) : IRequest<Result<AreaEligibilityCheckDto>>;

public sealed class CheckAreaEligibilityQueryHandler(
    IJobTaskRepository jobTaskRepository) : IRequestHandler<CheckAreaEligibilityQuery, Result<AreaEligibilityCheckDto>>
{
    public async Task<Result<AreaEligibilityCheckDto>> Handle(CheckAreaEligibilityQuery request, CancellationToken cancellationToken)
    {
        var isAssigned = await jobTaskRepository.IsDriverAssignedToAreaOnDateAsync(
            request.DriverId,
            request.AreaId,
            request.ScheduledDate,
            cancellationToken);

        if (isAssigned)
        {
            return new AreaEligibilityCheckDto(
                IsAssignedToArea: true,
                RequiresWarning: false,
                WarningMessage: null);
        }

        return new AreaEligibilityCheckDto(
            IsAssignedToArea: false,
            RequiresWarning: true,
            WarningMessage: $"Driver '{request.DriverId}' is not assigned to area '{request.AreaId}' on {request.ScheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}. You may override this warning upon assignment.");
    }
}
