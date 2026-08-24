using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Commands.CreateJobTask;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Application.Tests.Dispatch.TestDoubles;
using Nimpression.Domain.Enums;
using Xunit;

namespace Nimpression.Application.Tests.Dispatch.Commands;

public sealed class CreateJobTaskCommandHandlerTests
{
    private readonly FakeJobTaskRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeAuditSink _auditSink = new();
    private readonly CreateJobTaskCommandHandler _handler;

    public CreateJobTaskCommandHandlerTests()
    {
        _handler = new CreateJobTaskCommandHandler(_repo, _uow, _currentUser, _auditSink);
    }

    [Fact]
    public async Task Handle_ValidUnassignedTask_CreatesDraftTaskSuccessfully()
    {
        // Arrange
        var areaId = Guid.NewGuid();
        _repo.ExistingAreaIds.Add(areaId);

        var scheduledTime = DateTimeOffset.UtcNow.AddDays(1);
        var command = new CreateJobTaskCommand(
            "TSK-20260825-0001",
            "Pallet Freight Run",
            areaId,
            scheduledTime,
            TaskPriority.High,
            "Deliver 5 pallets",
            50m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        _repo.Tasks.Should().ContainKey(result.Value);
        var task = _repo.Tasks[result.Value];
        task.Title.Should().Be("Pallet Freight Run");
        task.Ref.Should().Be("TSK-20260825-0001");
        task.Status.Should().Be(JobTaskStatus.Draft);
        task.AreaId.Should().Be(areaId);
        task.Priority.Should().Be(TaskPriority.High);
        task.PlannedDistanceKm?.Value.Should().Be(50m);
        _uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AssignedTaskWithEligibleArea_CreatesAssignedTask()
    {
        // Arrange
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _repo.ExistingAreaIds.Add(areaId);
        _repo.ExistingDriverIds.Add(driverId);
        _repo.ExistingVehicleIds.Add(vehicleId);

        var scheduledTime = DateTimeOffset.UtcNow.AddDays(1);
        var scheduledDate = DateOnly.FromDateTime(scheduledTime.DateTime);
        _repo.DriverAreaAssignments.Add((driverId, areaId, scheduledDate));

        var command = new CreateJobTaskCommand(
            "TSK-ASSIGN-01",
            "Express Parcel Run",
            areaId,
            scheduledTime,
            TaskPriority.Medium,
            DriverId: driverId,
            VehicleId: vehicleId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var task = _repo.Tasks[result.Value];
        task.Status.Should().Be(JobTaskStatus.Assigned);
        task.DriverId.Should().Be(driverId);
        task.VehicleId.Should().Be(vehicleId);
        _auditSink.RecordedAudits.Should().BeEmpty("No override was performed");
    }

    [Fact]
    public async Task Handle_AssignedTaskWithAreaMismatch_WithoutOverride_Returns422Warning()
    {
        // Arrange
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _repo.ExistingAreaIds.Add(areaId);
        _repo.ExistingDriverIds.Add(driverId);
        _repo.ExistingVehicleIds.Add(vehicleId);
        // Driver is NOT assigned to areaId on scheduled date

        var scheduledTime = DateTimeOffset.UtcNow.AddDays(1);
        var command = new CreateJobTaskCommand(
            "TSK-MISMATCH-01",
            "Out of Area Task",
            areaId,
            scheduledTime,
            DriverId: driverId,
            VehicleId: vehicleId,
            OverrideAreaWarning: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: 422 且给出可越过的警告
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("area_mismatch_warning");
        _auditSink.RecordedAudits.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AssignedTaskWithAreaMismatch_WithOverride_SucceedsAndRecordsAudit()
    {
        // Arrange
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _repo.ExistingAreaIds.Add(areaId);
        _repo.ExistingDriverIds.Add(driverId);
        _repo.ExistingVehicleIds.Add(vehicleId);
        // Driver is NOT assigned to areaId

        var scheduledTime = DateTimeOffset.UtcNow.AddDays(1);
        var command = new CreateJobTaskCommand(
            "TSK-OVERRIDE-01",
            "Cross-area Task",
            areaId,
            scheduledTime,
            DriverId: driverId,
            VehicleId: vehicleId,
            OverrideAreaWarning: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: 越过警告成功，并写审计
        result.IsSuccess.Should().BeTrue();
        var task = _repo.Tasks[result.Value];
        task.Status.Should().Be(JobTaskStatus.Assigned);

        _auditSink.RecordedAudits.Should().ContainSingle(a => a.Action == "OverrideAreaWarning");
    }

    [Fact]
    public async Task Handle_NonExistentArea_Returns404NotFound()
    {
        // Arrange
        var command = new CreateJobTaskCommand(
            "TSK-NO-AREA",
            "Task",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("area_not_found");
    }
}
