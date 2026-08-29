using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Commands.DeleteArea;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Domain.Entities.Area;
using Xunit;

namespace Nimpression.Application.Tests.Areas.Commands;

public sealed class DeleteAreaCommandHandlerTests
{
    private readonly FakeAreaRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly DeleteAreaCommandHandler _handler;

    public DeleteAreaCommandHandlerTests()
    {
        _handler = new DeleteAreaCommandHandler(_repo, _uow, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_AreaWithNoActiveAssignments_DeletesSuccessfully()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "Auckland Central", "AKL-CBD");
        _repo.Areas[area.Id] = area;

        var command = new DeleteAreaCommand(area.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repo.Areas.Should().NotContainKey(area.Id);
        _uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NonExistentArea_Returns404NotFound()
    {
        // Arrange
        var command = new DeleteAreaCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("area_not_found");
    }

    [Fact]
    public async Task Handle_AreaWithActiveAssignments_Returns409Conflict()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "North Shore", "AKL-NS");
        _repo.Areas[area.Id] = area;

        var assignment = new AreaAssignment(
            Guid.NewGuid(),
            area.Id,
            Guid.NewGuid(),
            _dateTimeProvider.NzToday.AddDays(-10),
            null); // Active (no end date)
        _repo.Assignments[assignment.Id] = assignment;

        var command = new DeleteAreaCommand(area.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be("area_has_active_assignments");
    }
}
