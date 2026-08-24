using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.Commands.RefreshToken;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Commands;

public class RefreshTokenCommandHandlerTests
{
    private readonly IIdentityRepository _identityRepository = Substitute.For<IIdentityRepository>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditSink _auditSink = Substitute.For<IAuditSink>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly RefreshTokenCommandHandler _handler;
    private readonly DateTimeOffset _now = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    public RefreshTokenCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(_now);
        _handler = new RefreshTokenCommandHandler(
            _identityRepository,
            _jwtTokenGenerator,
            _unitOfWork,
            _auditSink,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_WithValidActiveRefreshToken_RotatesTokenAndReturnsNewTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = new EmailAddress("user@example.com");
        var user = new User(userId, email, "hash", UserRole.Driver, "Driver 1");

        var oldToken = new RefreshToken(
            Guid.NewGuid(),
            userId,
            "old_token_hash",
            _now.AddDays(5),
            "127.0.0.1",
            _now.AddDays(-2));

        _jwtTokenGenerator.HashRefreshToken("raw_old_token").Returns("old_token_hash");
        _identityRepository.GetRefreshTokenByHashAsync("old_token_hash", Arg.Any<CancellationToken>())
            .Returns(oldToken);

        _identityRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _jwtTokenGenerator.GenerateAccessToken(userId, "user@example.com", "Driver", "Driver 1")
            .Returns(("new_access_token", 900));

        _jwtTokenGenerator.GenerateRefreshToken(Arg.Any<string?>())
            .Returns(("new_raw_refresh_token", "new_token_hash", _now.AddDays(7)));

        var command = new RefreshTokenCommand("raw_old_token", "127.0.0.1", "Browser");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new_access_token");
        result.Value.RawRefreshToken.Should().Be("new_raw_refresh_token");

        oldToken.IsRevoked.Should().BeTrue();
        oldToken.RevokedAt.Should().Be(_now);
        oldToken.ReplacedById.Should().NotBeNull();

        await _identityRepository.Received(1).AddRefreshTokenAsync(
            Arg.Is<RefreshToken>(rt => rt.UserId == userId && rt.TokenHash == "new_token_hash"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var command = new RefreshTokenCommand("", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_TOKEN_MISSING");
    }

    [Fact]
    public async Task Handle_WithInvalidTokenHashNotFound_ReturnsUnauthorized()
    {
        // Arrange
        _jwtTokenGenerator.HashRefreshToken("invalid_token").Returns("non_existent_hash");
        _identityRepository.GetRefreshTokenByHashAsync("non_existent_hash", Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var command = new RefreshTokenCommand("invalid_token", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_TOKEN_INVALID");
    }

    [Fact]
    public async Task Handle_WithReplayedRevokedToken_RevokesAllUserTokensAndRecordsAuditEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var replayedToken = new RefreshToken(
            Guid.NewGuid(),
            userId,
            "replayed_hash",
            _now.AddDays(5),
            "127.0.0.1",
            _now.AddDays(-3));
        replayedToken.Revoke(_now.AddDays(-1)); // Already revoked

        var activeToken1 = new RefreshToken(Guid.NewGuid(), userId, "active_hash_1", _now.AddDays(6));
        var activeToken2 = new RefreshToken(Guid.NewGuid(), userId, "active_hash_2", _now.AddDays(6));

        _jwtTokenGenerator.HashRefreshToken("replayed_raw_token").Returns("replayed_hash");
        _identityRepository.GetRefreshTokenByHashAsync("replayed_hash", Arg.Any<CancellationToken>())
            .Returns(replayedToken);

        _identityRepository.GetActiveRefreshTokensByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<RefreshToken> { activeToken1, activeToken2 });

        var command = new RefreshTokenCommand("replayed_raw_token", "192.168.1.100", "Attacker");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_TOKEN_REVOKED");

        activeToken1.IsRevoked.Should().BeTrue();
        activeToken2.IsRevoked.Should().BeTrue();

        await _auditSink.Received(1).RecordAsync(
            Arg.Is<string>(e => e == "User"),
            Arg.Is<Guid?>(id => id == userId),
            Arg.Is<string>(a => a == "Security.RefreshTokenReplayDetected"),
            Arg.Is<string?>(b => b == null),
            Arg.Is<string?>(s => s != null && s.Contains("replayedTokenId") && s.Contains("192.168.1.100")),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiredToken = new RefreshToken(
            Guid.NewGuid(),
            userId,
            "expired_hash",
            _now.AddDays(-1), // Expired
            "127.0.0.1",
            _now.AddDays(-8));

        _jwtTokenGenerator.HashRefreshToken("expired_raw_token").Returns("expired_hash");
        _identityRepository.GetRefreshTokenByHashAsync("expired_hash", Arg.Any<CancellationToken>())
            .Returns(expiredToken);

        var command = new RefreshTokenCommand("expired_raw_token", "127.0.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_TOKEN_EXPIRED");
    }
}
