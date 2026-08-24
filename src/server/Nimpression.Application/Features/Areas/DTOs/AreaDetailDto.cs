namespace Nimpression.Application.Features.Areas.DTOs;

/// <summary>
/// 区域详情 DTO，包含活跃司机统计。
/// </summary>
public sealed record AreaDetailDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string? GeoJson,
    bool IsActive,
    int ActiveDriversCount);
