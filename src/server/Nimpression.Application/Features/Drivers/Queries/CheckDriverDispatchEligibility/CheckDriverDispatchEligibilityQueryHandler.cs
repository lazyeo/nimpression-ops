using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.Queries.CheckDriverDispatchEligibility;

/// <summary>
/// 检查司机派单资格查询处理器（F2.3）。
/// </summary>
public sealed class CheckDriverDispatchEligibilityQueryHandler(
    IDriverRepository driverRepository,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<CheckDriverDispatchEligibilityQuery, Result<DriverDispatchEligibilityDto>>
{
    public async Task<Result<DriverDispatchEligibilityDto>> Handle(
        CheckDriverDispatchEligibilityQuery request,
        CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId}' was not found.");
        }

        var referenceDate = request.ReferenceDate
            ?? dateTimeProvider?.NzToday
            ?? DateOnly.FromDateTime(DateTime.UtcNow);

        if (driver.Status != DriverStatus.Active)
        {
            return Error.Unprocessable(
                "driver_not_active",
                $"Driver '{driver.EmployeeNo}' status is {driver.Status} and cannot be dispatched.");
        }

        if (driver.IsLicenceExpired(referenceDate))
        {
            return Error.Unprocessable(
                "driver_licence_expired",
                $"Driver '{driver.EmployeeNo}' licence expired on {driver.LicenceExpiry:yyyy-MM-dd} and cannot be dispatched.");
        }

        return new DriverDispatchEligibilityDto(driver.Id, true, null);
    }
}
