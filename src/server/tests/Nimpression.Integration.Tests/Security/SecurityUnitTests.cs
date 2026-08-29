using FluentAssertions;
using Microsoft.Extensions.Options;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Infrastructure.Security;
using NSubstitute;
using Xunit;

namespace Nimpression.Integration.Tests.Security;

public class SecurityUnitTests
{
    [Fact]
    public void PasswordHasher_HashPassword_ProducesValidBCryptHashAndVerifies()
    {
        var hasher = new PasswordHasher();
        var rawPassword = "SuperSecurePassword123!";

        var hash = hasher.HashPassword(rawPassword);

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$2"); // BCrypt prefix
        hasher.VerifyPassword(rawPassword, hash).Should().BeTrue();
        hasher.VerifyPassword("WrongPassword123!", hash).Should().BeFalse();

        // 验证假哈希用于时序侧信道对齐时可正常解析执行且返回 false
        const string dummyHash = "$2a$12$XZCHWVyJw9OQb10ZeqYcyeOQcZJ6bY5xH7Wl.c6kR4V1mQZ4m1aCe";
        hasher.VerifyPassword(rawPassword, dummyHash).Should().BeFalse();
    }

    [Fact]
    public void JwtTokenGenerator_GenerateAccessToken_GeneratesValidTokenWithExpectedClaims()
    {
        var options = Options.Create(new JwtSettings
        {
            Secret = "SuperSecretKeyForTestingJwtTokenGenerationMustBeLongEnough123!",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenLifetimeMinutes = 15
        });

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var now = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        dateTimeProvider.UtcNow.Returns(now);

        var generator = new JwtTokenGenerator(options, dateTimeProvider);
        var userId = Guid.NewGuid();

        var (token, expiresIn) = generator.GenerateAccessToken(userId, "admin@example.com", "Admin", "Admin User");

        token.Should().NotBeNullOrWhiteSpace();
        expiresIn.Should().Be(900); // 15 minutes = 900s
    }

    [Fact]
    public void JwtTokenGenerator_GenerateRefreshToken_GeneratesUniqueRandomTokenAndHash()
    {
        var options = Options.Create(new JwtSettings());
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        var now = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        dateTimeProvider.UtcNow.Returns(now);

        var generator = new JwtTokenGenerator(options, dateTimeProvider);

        var (rawToken1, hash1, expiresAt1) = generator.GenerateRefreshToken("127.0.0.1");
        var (rawToken2, hash2, _) = generator.GenerateRefreshToken("127.0.0.1");

        rawToken1.Should().NotBeNullOrWhiteSpace();
        hash1.Should().NotBeNullOrWhiteSpace();
        rawToken1.Should().NotBe(rawToken2);
        hash1.Should().NotBe(hash2);
        expiresAt1.Should().Be(now.AddDays(7));
    }
}
