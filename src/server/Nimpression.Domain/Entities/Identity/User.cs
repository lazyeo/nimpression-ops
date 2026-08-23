using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Identity;

/// <summary>
/// 统一身份用户实体。承载系统认证、角色授权、个人设置与防爆破锁定逻辑。
/// </summary>
public sealed class User : AggregateRoot
{
    public EmailAddress Email { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? AvatarKey { get; private set; }
    public string Locale { get; private set; } = "en-NZ";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }

    private User()
    {
    }

    public User(
        Guid id,
        EmailAddress email,
        string passwordHash,
        UserRole role,
        string displayName,
        string locale = "en-NZ",
        DateTimeOffset? createdAt = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainValidationException("Password hash cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainValidationException("Display name cannot be empty.");
        }

        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        Status = UserStatus.Active;
        DisplayName = displayName.Trim();
        Locale = string.IsNullOrWhiteSpace(locale) ? "en-NZ" : locale.Trim();
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string displayName, string? avatarKey, string locale)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainValidationException("Display name cannot be empty.");
        }

        DisplayName = displayName.Trim();
        AvatarKey = string.IsNullOrWhiteSpace(avatarKey) ? null : avatarKey.Trim();
        Locale = string.IsNullOrWhiteSpace(locale) ? "en-NZ" : locale.Trim();
    }

    public void SetPasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new DomainValidationException("Password hash cannot be empty.");
        }

        PasswordHash = newPasswordHash;
    }

    public void ChangeRole(UserRole role)
    {
        Role = role;
    }

    public void SetStatus(UserStatus status)
    {
        Status = status;
    }

    public bool IsLockedOut(DateTimeOffset currentTime)
    {
        return LockoutEnd.HasValue && LockoutEnd.Value > currentTime;
    }

    public void RecordLoginSuccess(DateTimeOffset loginTime)
    {
        FailedLoginAttempts = 0;
        LockoutEnd = null;
        LastLoginAt = loginTime;
    }

    public void RecordLoginFailure(
        DateTimeOffset attemptTime,
        int maxFailedAttempts = 5,
        TimeSpan? lockoutDuration = null)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxFailedAttempts)
        {
            var duration = lockoutDuration ?? TimeSpan.FromMinutes(15);
            LockoutEnd = attemptTime.Add(duration);
        }
    }

    public void Unlock()
    {
        FailedLoginAttempts = 0;
        LockoutEnd = null;
    }
}
