namespace Nimpression.Application.Features.News.DTOs;

/// <summary>
/// 新闻公告已读统计 DTO（如「已读 7/10」及已读率）。
/// </summary>
public sealed record NewsReadStatsDto(
    Guid NewsPostId,
    int ReadCount,
    int TargetAudienceCount,
    double ReadRate);
