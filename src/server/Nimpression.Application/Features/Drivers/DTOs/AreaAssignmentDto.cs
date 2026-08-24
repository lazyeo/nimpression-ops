namespace Nimpression.Application.Features.Drivers.DTOs;

/// <summary>
/// 司机区域分配视图 DTO。
/// </summary>
public sealed record AreaAssignmentDto(
    Guid Id,
    Guid AreaId,
    string AreaName,
    string AreaCode,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive);
