using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Communications;

/// <summary>
/// 新闻公告聚合根。支持中英双语正文、受众划分、置顶与已读统计。
/// </summary>
public sealed class NewsPost : AggregateRoot
{
    public Guid AuthorUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string BodyEn { get; private set; } = string.Empty;
    public string BodyZh { get; private set; } = string.Empty;
    public NewsAudience Audience { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public bool Pinned { get; private set; }
    public bool IsActive { get; private set; }

    private NewsPost()
    {
    }

    public NewsPost(
        Guid id,
        Guid authorUserId,
        string title,
        string bodyEn,
        string bodyZh,
        NewsAudience audience,
        DateTimeOffset publishedAt,
        bool pinned = false,
        bool isActive = true) : base(id)
    {
        if (authorUserId == Guid.Empty)
        {
            throw new DomainValidationException("AuthorUserId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException("News title cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(bodyEn) && string.IsNullOrWhiteSpace(bodyZh))
        {
            throw new DomainValidationException("At least one language body (English or Chinese) must be provided.");
        }

        AuthorUserId = authorUserId;
        Title = title.Trim();
        BodyEn = bodyEn.Trim();
        BodyZh = bodyZh.Trim();
        Audience = audience;
        PublishedAt = publishedAt;
        Pinned = pinned;
        IsActive = isActive;

        AddDomainEvent(new NewsPublished(Id, audience, publishedAt));
    }

    public void UpdateContent(string title, string bodyEn, string bodyZh, NewsAudience audience)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException("News title cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(bodyEn) && string.IsNullOrWhiteSpace(bodyZh))
        {
            throw new DomainValidationException("At least one language body must be provided.");
        }

        Title = title.Trim();
        BodyEn = bodyEn.Trim();
        BodyZh = bodyZh.Trim();
        Audience = audience;
    }

    public void Pin(bool pinned) => Pinned = pinned;
    public void Archive() => IsActive = false;
    public void Restore() => IsActive = true;
}
