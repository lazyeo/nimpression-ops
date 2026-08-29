namespace Nimpression.Application.Features.Areas.DTOs;

/// <summary>
/// 区域列表查询过滤条件。
/// </summary>
public sealed record AreaFilter(
    string? SearchTerm = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20);
