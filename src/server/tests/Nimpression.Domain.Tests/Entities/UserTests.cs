using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class UserTests
{
    [Fact]
    public void User_initializes_with_valid_data()
    {
        var id = Guid.NewGuid();
        var email = new EmailAddress("user@nimpression.co.nz");
        var now = DateTimeOffset.UtcNow;
        var user = new User(id, email, "hashed_pw", UserRole.Driver, "John Doe", "en-NZ", now);

        Assert.Equal(id, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal("hashed_pw", user.PasswordHash);
        Assert.Equal(UserRole.Driver, user.Role);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal("John Doe", user.DisplayName);
        Assert.Equal("en-NZ", user.Locale);
        Assert.Equal(now, user.CreatedAt);
        Assert.False(user.IsLockedOut(now));
    }

    [Fact]
    public void User_throws_on_invalid_constructor_args()
    {
        var email = new EmailAddress("user@nimpression.co.nz");
        Assert.Throws<DomainValidationException>(() => new User(Guid.NewGuid(), email, "", UserRole.Admin, "Name"));
        Assert.Throws<DomainValidationException>(() => new User(Guid.NewGuid(), email, "pw", UserRole.Admin, "   "));
    }

    [Fact]
    public void User_lockout_and_login_failure_progression()
    {
        var user = new User(Guid.NewGuid(), new EmailAddress("u@test.com"), "pw", UserRole.Driver, "User");
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 4; i++)
        {
            user.RecordLoginFailure(now);
            Assert.False(user.IsLockedOut(now));
        }

        user.RecordLoginFailure(now, maxFailedAttempts: 5, lockoutDuration: TimeSpan.FromMinutes(15));
        Assert.True(user.IsLockedOut(now));
        Assert.True(user.IsLockedOut(now.AddMinutes(14)));
        Assert.False(user.IsLockedOut(now.AddMinutes(16)));

        user.Unlock();
        Assert.False(user.IsLockedOut(now));

        user.RecordLoginSuccess(now);
        Assert.Equal(now, user.LastLoginAt);
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public void User_profile_and_role_updates()
    {
        var user = new User(Guid.NewGuid(), new EmailAddress("u@test.com"), "pw", UserRole.Driver, "Old Name");
        user.UpdateProfile("New Name", "avatar123", "zh-CN");
        Assert.Equal("New Name", user.DisplayName);
        Assert.Equal("avatar123", user.AvatarKey);
        Assert.Equal("zh-CN", user.Locale);

        user.ChangeRole(UserRole.Dispatcher);
        Assert.Equal(UserRole.Dispatcher, user.Role);

        user.SetStatus(UserStatus.Suspended);
        Assert.Equal(UserStatus.Suspended, user.Status);

        user.SetPasswordHash("new_hash");
        Assert.Equal("new_hash", user.PasswordHash);
    }

    [Fact]
    public void RefreshToken_lifecycle()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(7);
        var token = new RefreshToken(Guid.NewGuid(), userId, "token_hash", expiresAt, "127.0.0.1", now);

        Assert.True(token.IsActive(now));
        Assert.False(token.IsExpired(now));
        Assert.False(token.IsRevoked);

        var replacedId = Guid.NewGuid();
        token.Revoke(now.AddDays(1), replacedId);
        Assert.True(token.IsRevoked);
        Assert.False(token.IsActive(now));
        Assert.Equal(replacedId, token.ReplacedById);
    }
}
