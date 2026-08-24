using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.Commands.DeactivateUser;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Commands;

public class DeactivateUserCommandHandlerTests
{
    private readonly IIdentityRepository _identityRepository = Substitute.For<IIdentityRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly DeactivateUserCommandHandler _handler;
    private readonly DateTimeOffset _now = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    public DeactivateUserCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(_now);
        _handler = new DeactivateUserCommandHandler(
            _identityRepository,
            _unitOfWork,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_WhenUserExists_DeactivatesUserAndRevokesAllActiveTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(userId, new EmailAddress("driver@example.com"), "hash", UserRole.Driver, "Driver");

        var token1 = new RefreshToken(Guid.NewGuid(), userId, "hash1", _now.AddDays(7));
        var token2 = new RefreshToken(Guid.NewGuid(), userId, "hash2", _now.AddDays(7));

        _identityRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _identityRepository.GetActiveRefreshTokensByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<RefreshToken> { token1, token2 });

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Inactive);
        token1.IsRevoked.Should().BeTrue();
        token2.IsRevoked.Should().BeTrue();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _identityRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var command = new DeactivateUserCommand(userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("USER_NOT_FOUND");
    }
}
