namespace Nimpression.Application.Features.Drivers.DTOs;

/// <summary>
/// 司机派单资格校验 DTO（F2.3）。
/// </summary>
public sealed record DriverDispatchEligibilityDto(
    Guid DriverId,
    bool CanBeDispatched,
    string? Reason);
