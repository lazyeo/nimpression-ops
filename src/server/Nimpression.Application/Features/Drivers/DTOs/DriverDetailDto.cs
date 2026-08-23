using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.DTOs;

/// <summary>
/// 司机详情视图 DTO。
/// </summary>
public sealed record DriverDetailDto(
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
    decimal HourlyRateAmount,
    string HourlyRateCurrency,
    decimal PerTripRateAmount,
    string PerTripRateCurrency,
    decimal PerKmRateAmount,
    string PerKmRateCurrency,
    string Phone,
    string Address,
    string EmergencyContact,
    string Locale,
    string? AvatarKey,
    string? AvatarUrl,
    IReadOnlyList<AreaAssignmentDto> AreaAssignments);
