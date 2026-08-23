using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;

namespace Nimpression.Application.Features.Drivers.Queries.GetLicenceAlerts;

/// <summary>
/// 获取驾照到期预警列表查询处理器（F2.3）。
/// </summary>
public sealed class GetLicenceAlertsQueryHandler(
    IDriverRepository driverRepository,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<GetLicenceAlertsQuery, Result<List<DriverLicenceAlertDto>>>
{
    public async Task<Result<List<DriverLicenceAlertDto>>> Handle(
        GetLicenceAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var referenceDate = dateTimeProvider?.NzToday ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var alerts = await driverRepository.GetExpiringLicencesAsync(
            referenceDate,
            request.DaysThreshold,
            cancellationToken);

        return alerts;
    }
}
