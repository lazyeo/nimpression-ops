using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Commands.AssignJobTask;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Application.Tests.Dispatch.TestDoubles;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;
using Xunit;

namespace Nimpression.Application.Tests.Dispatch.Commands;

public sealed class AssignJobTaskCommandHandlerTests
{
    private readonly FakeJobTaskRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly FakeAuditSink _auditSink = new();
    private readonly AssignJobTaskCommandHandler _handler;

    public AssignJobTaskCommandHandlerTests()
    {
        _handler = new AssignJobTaskCommandHandler(_repo, _uow, _dateTimeProvider, _auditSink);
    }

    [Fact]
    public async Task Handle_ValidDraftTask_AssignsDriverAndVehicle()
    {
        // Arrange
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _repo.ExistingAreaIds.Add(areaId);
        _repo.ExistingDriverIds.Add(driverId);
        _repo.ExistingVehicleIds.Add(vehicleId);

        var task = new JobTask(Guid.NewGuid(), "TSK-001", "Delivery", areaId, _dateTimeProvider.UtcNow.AddHours(2), Guid.NewGuid());
        _repo.Tasks[task.Id] = task;

        var scheduledDate = DateOnly.FromDateTime(task.ScheduledFor.DateTime);
        _repo.DriverAreaAssignments.Add((driverId, areaId, scheduledDate));

        var command = new AssignJobTaskCommand(task.Id, driverId, vehicleId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Status.Should().Be(JobTaskStatus.Assigned);
        task.DriverId.Should().Be(driverId);
        task.VehicleId.Should().Be(vehicleId);
        _uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AreaMismatch_WithoutOverride_Returns422Unprocessable()
    {
        // Arrange
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _repo.ExistingAreaIds.Add(areaId);
        _repo.ExistingDriverIds.Add(driverId);
        _repo.ExistingVehicleIds.Add(vehicleId);

        var task = new JobTask(Guid.NewGuid(), "TSK-002", "Delivery", areaId, _dateTimeProvider.UtcNow.AddHours(2), Guid.NewGuid());
        _repo.Tasks[task.Id] = task;
        // Driver not in area

        var command = new AssignJobTaskCommand(task.Id, driverId, vehicleId, OverrideAreaWarning: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("area_mismatch_warning");
        task.Status.Should().Be(JobTaskStatus.Draft);
    }

    [Fact]
    public async Task Handle_AreaMismatch_WithOverride_SucceedsAndAudits()
    {
        // Arrange
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _repo.ExistingAreaIds.Add(areaId);
        _repo.ExistingDriverIds.Add(driverId);
        _repo.ExistingVehicleIds.Add(vehicleId);

        var task = new JobTask(Guid.NewGuid(), "TSK-003", "Delivery", areaId, _dateTimeProvider.UtcNow.AddHours(2), Guid.NewGuid());
        _repo.Tasks[task.Id] = task;

        var command = new AssignJobTaskCommand(task.Id, driverId, vehicleId, OverrideAreaWarning: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Status.Should().Be(JobTaskStatus.Assigned);
        _auditSink.RecordedAudits.Should().ContainSingle(a => a.Action == "OverrideAreaWarning" && a.EntityId == task.Id);
    }

    [Fact]
    public async Task Handle_AssignCompletedTask_Returns422InvalidTransition()
    {
        // Arrange
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _repo.ExistingAreaIds.Add(areaId);
        _repo.ExistingDriverIds.Add(driverId);
        _repo.ExistingVehicleIds.Add(vehicleId);

        var task = new JobTask(Guid.NewGuid(), "TSK-004", "Delivery", areaId, _dateTimeProvider.UtcNow.AddHours(2), Guid.NewGuid());
        task.Assign(driverId, vehicleId, null, _dateTimeProvider.UtcNow);
        task.Acknowledge(_dateTimeProvider.UtcNow);
        task.Start(_dateTimeProvider.UtcNow);
        task.Complete(_dateTimeProvider.UtcNow);
        _repo.Tasks[task.Id] = task;

        var command = new AssignJobTaskCommand(task.Id, driverId, vehicleId, OverrideAreaWarning: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("invalid_task_transition");
    }
}
