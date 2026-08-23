using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.DTOs;

/// <summary>
/// 司机列表摘要视图 DTO（支持投影优化）。
/// </summary>
public sealed record DriverSummaryDto(
    Guid Id,
    Guid UserId,
    string EmployeeNo,
    string DisplayName,
    string Email,
    string LicenceClass,
    DateOnly LicenceExpiry,
    bool IsLicenceExpiringSoon,
    bool IsLicenceExpired,
    int DaysUntilLicenceExpiry,
    DriverStatus Status,
    DateOnly HiredOn,
    decimal HourlyRate,
    decimal PerTripRate,
    decimal PerKmRate,
    IReadOnlyList<string> AssignedAreaNames,
    IReadOnlyList<Guid> ActiveAreaIds,
    string? AvatarUrl = null);
