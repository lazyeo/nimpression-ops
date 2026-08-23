using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.DTOs;

/// <summary>
/// 驾照到期预警 DTO（F2.3）。
/// </summary>
public sealed record DriverLicenceAlertDto(
    Guid DriverId,
    Guid UserId,
    string EmployeeNo,
    string DisplayName,
    string LicenceClass,
    DateOnly LicenceExpiry,
    int DaysUntilExpiry,
    bool IsExpired,
    DriverStatus Status);
