using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.Commands.ChangePassword;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Identity.Commands;

public class ChangePasswordCommandHandlerTests
{
    private readonly IIdentityRepository _identityRepository = Substitute.For<IIdentityRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        _handler = new ChangePasswordCommandHandler(
            _identityRepository,
            _passwordHasher,
            _currentUser,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_WhenUserChangesOwnPasswordWithCorrectCurrentPassword_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(userId, new EmailAddress("driver@example.com"), "old_hash", UserRole.Driver, "Driver");

        _currentUser.UserId.Returns(userId);
        _currentUser.Role.Returns(UserRole.Driver);

        _identityRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("CurrentPass123!", "old_hash").Returns(true);
        _passwordHasher.HashPassword("NewStrongPass123!").Returns("new_hash");

        var command = new ChangePasswordCommand(userId, "CurrentPass123!", "NewStrongPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new_hash");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNonAdminChangesAnotherUsersPassword_ReturnsForbidden()
    {
        // Arrange
        var ownUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _currentUser.UserId.Returns(ownUserId);
        _currentUser.Role.Returns(UserRole.Driver);

        var command = new ChangePasswordCommand(targetUserId, "CurrentPass123!", "NewStrongPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_FORBIDDEN");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAdminChangesAnotherUsersPassword_Succeeds()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var targetUser = new User(targetUserId, new EmailAddress("target@example.com"), "old_hash", UserRole.Driver, "Target");

        _currentUser.UserId.Returns(adminUserId);
        _currentUser.Role.Returns(UserRole.Admin);

        _identityRepository.GetUserByIdAsync(targetUserId, Arg.Any<CancellationToken>())
            .Returns(targetUser);

        _passwordHasher.VerifyPassword("CurrentPass123!", "old_hash").Returns(true);
        _passwordHasher.HashPassword("NewStrongPass123!").Returns("new_hash");

        var command = new ChangePasswordCommand(targetUserId, "CurrentPass123!", "NewStrongPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        targetUser.PasswordHash.Should().Be("new_hash");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithIncorrectCurrentPassword_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(userId, new EmailAddress("user@example.com"), "old_hash", UserRole.Dispatcher, "Dispatcher");

        _currentUser.UserId.Returns(userId);
        _currentUser.Role.Returns(UserRole.Dispatcher);

        _identityRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("WrongCurrentPass!", "old_hash").Returns(false);

        var command = new ChangePasswordCommand(userId, "WrongCurrentPass!", "NewStrongPass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
