namespace Nimpression.Application.Features.Areas.DTOs;

/// <summary>
/// 区域概要 DTO。
/// </summary>
public sealed record AreaDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string? GeoJson,
    bool IsActive);
