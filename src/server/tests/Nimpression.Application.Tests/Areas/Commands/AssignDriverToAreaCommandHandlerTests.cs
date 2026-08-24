using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Commands.AssignDriverToArea;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Domain.Entities.Area;
using Xunit;

namespace Nimpression.Application.Tests.Areas.Commands;

public sealed class AssignDriverToAreaCommandHandlerTests
{
    private readonly FakeAreaRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly AssignDriverToAreaCommandHandler _handler;

    public AssignDriverToAreaCommandHandlerTests()
    {
        _handler = new AssignDriverToAreaCommandHandler(_repo, _uow);
    }

    [Fact]
    public async Task Handle_ValidNonOverlappingAssignment_SucceedsAndReturnsId()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "Auckland Central", "AKL-CBD");
        _repo.Areas[area.Id] = area;

        var driverId = Guid.NewGuid();
        _repo.ExistingDriverIds.Add(driverId);

        var command = new AssignDriverToAreaCommand(
            driverId,
            area.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _repo.Assignments.Should().ContainKey(result.Value);
        var created = _repo.Assignments[result.Value];
        created.DriverId.Should().Be(driverId);
        created.AreaId.Should().Be(area.Id);
        created.EffectiveFrom.Should().Be(new DateOnly(2026, 1, 1));
        created.EffectiveTo.Should().Be(new DateOnly(2026, 6, 30));
        _uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OverlappingAssignment_Returns422UnprocessableAndPointsOutConflictingInterval()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "Auckland Central", "AKL-CBD");
        _repo.Areas[area.Id] = area;

        var driverId = Guid.NewGuid();
        _repo.ExistingDriverIds.Add(driverId);

        // 已有 2026-01-01 .. 2026-06-30 的分配
        var existingAssignment = new AreaAssignment(
            Guid.NewGuid(),
            area.Id,
            driverId,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
        _repo.Assignments[existingAssignment.Id] = existingAssignment;

        // 请求重叠区间 2026-05-01 .. 2026-12-31
        var command = new AssignDriverToAreaCommand(
            driverId,
            area.Id,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 12, 31));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("area_assignment_overlap");
        result.Error.Message.Should().Contain("2026-01-01..2026-06-30");
        result.Error.Message.Should().Contain("2026-05-01..2026-12-31");
        result.Error.Details.Should().NotBeNull();
        result.Error.Details!["conflictPeriod"].Should().Contain("2026-01-01..2026-06-30");
        result.Error.Details["requestedPeriod"].Should().Contain("2026-05-01..2026-12-31");
    }

    [Fact]
    public async Task Handle_OpenEndedOverlappingAssignment_Returns422Unprocessable()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "North Shore", "AKL-NS");
        _repo.Areas[area.Id] = area;

        var driverId = Guid.NewGuid();
        _repo.ExistingDriverIds.Add(driverId);

        // 已有 2026-07-01 .. open 的分配
        var existingAssignment = new AreaAssignment(
            Guid.NewGuid(),
            area.Id,
            driverId,
            new DateOnly(2026, 7, 1),
            null);
        _repo.Assignments[existingAssignment.Id] = existingAssignment;

        // 请求 2026-08-01 .. 2026-09-01
        var command = new AssignDriverToAreaCommand(
            driverId,
            area.Id,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 1));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("area_assignment_overlap");
        result.Error.Message.Should().Contain("2026-07-01..open");
    }

    [Fact]
    public async Task Handle_NonOverlappingSubsequentAssignment_Succeeds()
    {
        // Arrange
        var area = new Area(Guid.NewGuid(), "West Auckland", "AKL-WEST");
        _repo.Areas[area.Id] = area;

        var driverId = Guid.NewGuid();
        _repo.ExistingDriverIds.Add(driverId);

        // 已有 2026-01-01 .. 2026-06-30 的分配
        var existingAssignment = new AreaAssignment(
            Guid.NewGuid(),
            area.Id,
            driverId,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 6, 30));
        _repo.Assignments[existingAssignment.Id] = existingAssignment;

        // 请求不重叠区间 2026-07-01 .. 2026-12-31
        var command = new AssignDriverToAreaCommand(
            driverId,
            area.Id,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 12, 31));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DifferentAreaSameDriver_DoesNotConflict()
    {
        // Arrange
        var area1 = new Area(Guid.NewGuid(), "Area 1", "A1");
        var area2 = new Area(Guid.NewGuid(), "Area 2", "A2");
        _repo.Areas[area1.Id] = area1;
        _repo.Areas[area2.Id] = area2;

        var driverId = Guid.NewGuid();
        _repo.ExistingDriverIds.Add(driverId);

        // 司机在 Area1 有 2026-01-01..2026-12-31
        var existingAssignment = new AreaAssignment(
            Guid.NewGuid(),
            area1.Id,
            driverId,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));
        _repo.Assignments[existingAssignment.Id] = existingAssignment;

        // 在 Area2 分配相同时间段，允许（同一司机可服务多个不同区域）
        var command = new AssignDriverToAreaCommand(
            driverId,
            area2.Id,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
