using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.DTOs;

/// <summary>
/// 新闻公告详情 DTO。
/// </summary>
public sealed record NewsPostDetailDto(
    Guid Id,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Title,
    string BodyEn,
    string BodyZh,
    NewsAudience Audience,
    DateTimeOffset PublishedAt,
    bool Pinned,
    bool IsActive,
    bool IsRead,
    DateTimeOffset? ReadAt);
