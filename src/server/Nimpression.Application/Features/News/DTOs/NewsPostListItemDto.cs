using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.DTOs;

/// <summary>
/// 新闻公告列表项投影 DTO。
/// </summary>
public sealed record NewsPostListItemDto(
    Guid Id,
    string Title,
    NewsAudience Audience,
    DateTimeOffset PublishedAt,
    bool Pinned,
    bool IsActive,
    bool IsRead,
    DateTimeOffset? ReadAt);
