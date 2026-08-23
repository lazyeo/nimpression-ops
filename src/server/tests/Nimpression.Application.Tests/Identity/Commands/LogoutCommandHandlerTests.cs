using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.Commands.Logout;
using Nimpression.Domain.Entities.Identity;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Commands;

public class LogoutCommandHandlerTests
{
    private readonly IIdentityRepository _identityRepository = Substitute.For<IIdentityRepository>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly LogoutCommandHandler _handler;
    private readonly DateTimeOffset _now = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    public LogoutCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(_now);
        _handler = new LogoutCommandHandler(
            _identityRepository,
            _jwtTokenGenerator,
            _unitOfWork,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_WithActiveToken_RevokesTokenAndSavesChanges()
    {
        // Arrange
        var token = new RefreshToken(Guid.NewGuid(), Guid.NewGuid(), "hash", _now.AddDays(7));
        _jwtTokenGenerator.HashRefreshToken("raw_token").Returns("hash");
        _identityRepository.GetRefreshTokenByHashAsync("hash", Arg.Any<CancellationToken>())
            .Returns(token);

        var command = new LogoutCommand("raw_token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.IsRevoked.Should().BeTrue();
        token.RevokedAt.Should().Be(_now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNullOrEmptyToken_ReturnsSuccessGracefully()
    {
        // Arrange
        var command = new LogoutCommand(null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
