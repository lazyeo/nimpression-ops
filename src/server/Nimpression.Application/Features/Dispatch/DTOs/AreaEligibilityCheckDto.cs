namespace Nimpression.Application.Features.Dispatch.DTOs;

/// <summary>
/// 区域派单资格校验结果 DTO（F4.3）。
/// </summary>
public sealed record AreaEligibilityCheckDto(
    bool IsAssignedToArea,
    bool RequiresWarning,
    string? WarningMessage);
