using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Commands.UpdateArea;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Domain.Entities.Area;
using Xunit;

namespace Nimpression.Application.Tests.Areas.Commands;

public sealed class UpdateAreaCommandHandlerTests
{
    private readonly FakeAreaRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly UpdateAreaCommandHandler _handler;

    public UpdateAreaCommandHandlerTests()
    {
        _handler = new UpdateAreaCommandHandler(_repo, _uow);
    }

    [Fact]
    public async Task Handle_ExistingArea_UpdatesDetailsSuccessfully()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "Old Name", "OLD-CODE", "Old desc", null, true);
        _repo.Areas[area.Id] = area;

        var command = new UpdateAreaCommand(
            area.Id,
            "New Name",
            "NEW-CODE",
            "New desc",
            "{\"type\":\"Polygon\"}",
            false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        area.Name.Should().Be("New Name");
        area.Code.Should().Be("NEW-CODE");
        area.Description.Should().Be("New desc");
        area.GeoJson.Should().Be("{\"type\":\"Polygon\"}");
        area.IsActive.Should().BeFalse();
        _uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NonExistentArea_Returns404NotFound()
    {
        // Arrange
        var command = new UpdateAreaCommand(Guid.NewGuid(), "New Name", "NEW-CODE");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("area_not_found");
    }

    [Fact]
    public async Task Handle_DuplicateCodeOnSave_Returns409Conflict()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "Area A", "CODE-A");
        _repo.Areas[area.Id] = area;

        _uow.ThrowOnSave = true;
        _uow.ExceptionToThrow = new InvalidOperationException("duplicate key value violates unique constraint 23505");

        var command = new UpdateAreaCommand(area.Id, "Area A Modified", "CODE-B");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be("area_code_conflict");
    }
}
