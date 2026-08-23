using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Identity;

/// <summary>
/// 刷新令牌实体，支持轮转与防重放撤销追踪。
/// </summary>
public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedById { get; private set; }
    public string? CreatedByIp { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? createdByIp = null,
        DateTimeOffset? createdAt = null) : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("UserId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainValidationException("TokenHash cannot be empty.");
        }

        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public bool IsExpired(DateTimeOffset currentTime) => currentTime >= ExpiresAt;

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsActive(DateTimeOffset currentTime) => !IsRevoked && !IsExpired(currentTime);

    public void Revoke(DateTimeOffset revokedAt, Guid? replacedById = null)
    {
        RevokedAt = revokedAt;
        ReplacedById = replacedById;
    }
}
