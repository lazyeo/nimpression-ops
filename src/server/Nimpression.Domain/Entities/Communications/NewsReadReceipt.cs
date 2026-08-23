using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Communications;

/// <summary>
/// 新闻公告已读回执实体。
/// </summary>
public sealed class NewsReadReceipt : Entity
{
    public Guid NewsPostId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset ReadAt { get; private set; }

    private NewsReadReceipt()
    {
    }

    public NewsReadReceipt(
        Guid id,
        Guid newsPostId,
        Guid userId,
        DateTimeOffset readAt) : base(id)
    {
        if (newsPostId == Guid.Empty)
        {
            throw new DomainValidationException("NewsPostId cannot be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("UserId cannot be empty.");
        }

        NewsPostId = newsPostId;
        UserId = userId;
        ReadAt = readAt;
    }
}
