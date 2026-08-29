namespace Nimpression.Application.Features.Areas.DTOs;

/// <summary>
/// 司机区域分配 DTO。
/// </summary>
public sealed record AreaAssignmentDto(
    Guid Id,
    Guid AreaId,
    string AreaName,
    string AreaCode,
    Guid DriverId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive);
