using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.Queries.GetUserById;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Queries;

public class GetUserByIdQueryHandlerTests
{
    private readonly IIdentityRepository _identityRepository = Substitute.For<IIdentityRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly GetUserByIdQueryHandler _handler;
    private readonly DateTimeOffset _now = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    public GetUserByIdQueryHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(_now);
        _handler = new GetUserByIdQueryHandler(
            _identityRepository,
            _currentUser,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_WhenDriverQueriesOwnProfile_ReturnsUserDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(userId, new EmailAddress("driver@example.com"), "hash", UserRole.Driver, "Driver Name");

        _currentUser.UserId.Returns(userId);
        _currentUser.Role.Returns(UserRole.Driver);

        _identityRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var query = new GetUserByIdQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(userId);
        result.Value.Email.Should().Be("driver@example.com");
        result.Value.DisplayName.Should().Be("Driver Name");
        result.Value.Role.Should().Be(UserRole.Driver);
    }

    [Fact]
    public async Task Handle_WhenDriverQueriesAnotherUserProfile_ReturnsForbidden()
    {
        // Arrange
        var ownUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();

        _currentUser.UserId.Returns(ownUserId);
        _currentUser.Role.Returns(UserRole.Driver);

        var query = new GetUserByIdQuery(anotherUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_FORBIDDEN");
        result.Error.Message.Should().Contain("Cannot access another user's profile");
    }

    [Fact]
    public async Task Handle_WhenAdminQueriesAnyUserProfile_ReturnsUserDto()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var targetUser = new User(targetUserId, new EmailAddress("driver@example.com"), "hash", UserRole.Driver, "Target Driver");

        _currentUser.UserId.Returns(adminUserId);
        _currentUser.Role.Returns(UserRole.Admin);

        _identityRepository.GetUserByIdAsync(targetUserId, Arg.Any<CancellationToken>())
            .Returns(targetUser);

        var query = new GetUserByIdQuery(targetUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(targetUserId);
        result.Value.Email.Should().Be("driver@example.com");
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _currentUser.UserId.Returns(adminUserId);
        _currentUser.Role.Returns(UserRole.Admin);

        _identityRepository.GetUserByIdAsync(targetUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var query = new GetUserByIdQuery(targetUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("USER_NOT_FOUND");
    }
}
