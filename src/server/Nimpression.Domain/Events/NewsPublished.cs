using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;

namespace Nimpression.Domain.Events;

/// <summary>
/// 新闻公告已发布事件。
/// </summary>
public sealed record NewsPublished(
    Guid NewsPostId,
    NewsAudience Audience,
    DateTimeOffset OccurredAt) : IDomainEvent;
