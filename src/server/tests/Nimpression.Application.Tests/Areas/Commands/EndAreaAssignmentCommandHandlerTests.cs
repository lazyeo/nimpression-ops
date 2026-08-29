using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Commands.EndAreaAssignment;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Domain.Entities.Area;
using Xunit;

namespace Nimpression.Application.Tests.Areas.Commands;

public sealed class EndAreaAssignmentCommandHandlerTests
{
    private readonly FakeAreaRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly EndAreaAssignmentCommandHandler _handler;

    public EndAreaAssignmentCommandHandlerTests()
    {
        _handler = new EndAreaAssignmentCommandHandler(_repo, _uow);
    }

    [Fact]
    public async Task Handle_ExistingAssignment_SetsEffectiveToSuccessfully()
    {
        // Arrange
        var assignment = new AreaAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            null);
        _repo.Assignments[assignment.Id] = assignment;

        var command = new EndAreaAssignmentCommand(assignment.Id, new DateOnly(2026, 6, 30));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        assignment.EffectiveTo.Should().Be(new DateOnly(2026, 6, 30));
        _uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NonExistentAssignment_Returns404NotFound()
    {
        // Arrange
        var command = new EndAreaAssignmentCommand(Guid.NewGuid(), new DateOnly(2026, 6, 30));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("area_assignment_not_found");
    }

    [Fact]
    public async Task Handle_EffectiveToEarlierThanEffectiveFrom_Returns422Unprocessable()
    {
        // Arrange
        var assignment = new AreaAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 6, 1),
            null);
        _repo.Assignments[assignment.Id] = assignment;

        var command = new EndAreaAssignmentCommand(assignment.Id, new DateOnly(2026, 1, 1)); // Earlier than from

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
    }
}
