using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.DTOs;

/// <summary>
/// 新闻公告列表查询筛选参数。
/// </summary>
public sealed record NewsListFilter(
    NewsAudience? Audience = null,
    bool? IsPinned = null,
    bool? IsActive = true,
    int Page = 1,
    int PageSize = 20);
