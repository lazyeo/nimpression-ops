using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Commands.MarkNewsAsRead;
using Nimpression.Application.Tests.News.TestDoubles;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.News.Commands;

public class MarkNewsAsReadCommandHandlerTests
{
    private readonly FakeNewsRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly Guid _userId = Guid.NewGuid();

    public MarkNewsAsReadCommandHandlerTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(_userId);
        _currentUser.Role.Returns(UserRole.Driver);
    }

    [Fact]
    public async Task Handle_FirstTimeRead_RecordsReadReceiptSuccessfully()
    {
        // Arrange
        var post = new NewsPost(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            "Body En",
            "Body Zh",
            NewsAudience.All,
            _dateTimeProvider.UtcNow);
        _repo.Posts[post.Id] = post;

        var handler = new MarkNewsAsReadCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        var command = new MarkNewsAsReadCommand(post.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _uow.SaveChangesCallCount.Should().Be(1);
        _repo.ReadReceipts.Should().ContainSingle(r => r.NewsPostId == post.Id && r.UserId == _userId);
    }

    [Fact]
    public async Task Handle_NewsNotFound_Returns404NotFound()
    {
        // Arrange
        var handler = new MarkNewsAsReadCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        var command = new MarkNewsAsReadCommand(Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_Returns401Unauthorized()
    {
        // Arrange
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.UserId.Returns((Guid?)null);

        var handler = new MarkNewsAsReadCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        var command = new MarkNewsAsReadCommand(Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
    }

    [Fact]
    public async Task Handle_DuplicateRead_WhenDatabaseThrowsUniqueConstraint23505_ReturnsSuccessIdempotently()
    {
        // Arrange: 模拟同一人重复打开时底层抛出 23505 唯一约束异常
        var post = new NewsPost(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Title",
            "Body En",
            "Body Zh",
            NewsAudience.All,
            _dateTimeProvider.UtcNow);
        _repo.Posts[post.Id] = post;

        _uow.ThrowOnSave = true;
        _uow.ExceptionToThrow = new InvalidOperationException("duplicate key value violates unique constraint \"IX_NewsReadReceipts_NewsPostId_UserId\" 23505");

        var handler = new MarkNewsAsReadCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        var command = new MarkNewsAsReadCommand(post.Id);

        // Act: 重复打开触发唯一约束冲突
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: 幂等返回成功，严禁先查后写
        result.IsSuccess.Should().BeTrue();
    }
}
