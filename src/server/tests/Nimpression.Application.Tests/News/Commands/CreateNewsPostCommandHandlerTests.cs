using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Commands.CreateNewsPost;
using Nimpression.Application.Tests.News.TestDoubles;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.News.Commands;

public class CreateNewsPostCommandHandlerTests
{
    private readonly FakeNewsRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();

    public CreateNewsPostCommandHandlerTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(Guid.NewGuid());
        _currentUser.Role.Returns(UserRole.Admin);
    }

    [Fact]
    public async Task Handle_ValidRequestByAdmin_PublishesNewsSuccessfully()
    {
        // Arrange
        var handler = new CreateNewsPostCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        var command = new CreateNewsPostCommand(
            "System Maintenance Notice",
            "System will be updated at midnight.",
            "系统将于午夜进行维护升级。",
            NewsAudience.All,
            true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _uow.SaveChangesCallCount.Should().Be(1);

        var saved = _repo.Posts[result.Value];
        saved.Should().NotBeNull();
        saved.Title.Should().Be("System Maintenance Notice");
        saved.BodyEn.Should().Be("System will be updated at midnight.");
        saved.BodyZh.Should().Be("系统将于午夜进行维护升级。");
        saved.Audience.Should().Be(NewsAudience.All);
        saved.Pinned.Should().BeTrue();
        saved.IsActive.Should().BeTrue();
        saved.PublishedAt.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_Returns401Unauthorized()
    {
        // Arrange
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.UserId.Returns((Guid?)null);

        var handler = new CreateNewsPostCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        var command = new CreateNewsPostCommand(
            "Title",
            "Body En",
            "Body Zh",
            NewsAudience.All);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Driver)]
    [InlineData(UserRole.Dispatcher)]
    public async Task Handle_NonAdminUser_Returns403Forbidden(UserRole role)
    {
        // Arrange
        _currentUser.Role.Returns(role);

        var handler = new CreateNewsPostCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        var command = new CreateNewsPostCommand(
            "Title",
            "Body En",
            "Body Zh",
            NewsAudience.All);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Theory]
    [InlineData("", "Body En", "Body Zh")]
    [InlineData("Title", "", "Body Zh")]
    [InlineData("Title", "   ", "Body Zh")]
    [InlineData("Title", "Body En", "")]
    [InlineData("Title", "Body En", "   ")]
    public async Task Handle_MissingRequiredFields_Returns422Unprocessable(string title, string bodyEn, string bodyZh)
    {
        // Arrange
        var handler = new CreateNewsPostCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        var command = new CreateNewsPostCommand(
            title,
            bodyEn,
            bodyZh,
            NewsAudience.All);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
    }
}
