using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;

namespace Nimpression.Application.Features.Drivers.Queries.GetDriversList;

/// <summary>
/// 司机列表分页查询处理器（F2.1）。
/// </summary>
public sealed class GetDriversListQueryHandler(
    IDriverRepository driverRepository,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<GetDriversListQuery, Result<PagedResult<DriverSummaryDto>>>
{
    public async Task<Result<PagedResult<DriverSummaryDto>>> Handle(
        GetDriversListQuery request,
        CancellationToken cancellationToken)
    {
        var referenceDate = dateTimeProvider?.NzToday ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await driverRepository.GetDriversPagedAsync(request.Filter, referenceDate, cancellationToken);
        return result;
    }
}
