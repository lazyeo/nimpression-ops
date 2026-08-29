using FluentAssertions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Commands.AcknowledgeJobTask;
using Nimpression.Application.Features.Dispatch.Commands.CancelJobTask;
using Nimpression.Application.Features.Dispatch.Commands.CompleteJobTask;
using Nimpression.Application.Features.Dispatch.Commands.StartJobTask;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Application.Tests.Dispatch.TestDoubles;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;
using Xunit;

namespace Nimpression.Application.Tests.Dispatch.Commands;

public sealed class JobTaskLifecycleCommandHandlerTests
{
    private readonly FakeJobTaskRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly FakeCurrentUser _currentUser = new();

    private readonly AcknowledgeJobTaskCommandHandler _ackHandler;
    private readonly StartJobTaskCommandHandler _startHandler;
    private readonly CompleteJobTaskCommandHandler _completeHandler;
    private readonly CancelJobTaskCommandHandler _cancelHandler;

    public JobTaskLifecycleCommandHandlerTests()
    {
        _ackHandler = new AcknowledgeJobTaskCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        _startHandler = new StartJobTaskCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        _completeHandler = new CompleteJobTaskCommandHandler(_repo, _uow, _currentUser, _dateTimeProvider);
        _cancelHandler = new CancelJobTaskCommandHandler(_repo, _uow, _dateTimeProvider);
    }

    [Fact]
    public async Task HappyPath_DraftToAssignedToAcknowledgedToInProgressToCompleted()
    {
        // 1. Task in Assigned status
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var task = new JobTask(Guid.NewGuid(), "TSK-LIFE-01", "Full Run", Guid.NewGuid(), _dateTimeProvider.UtcNow, Guid.NewGuid());
        task.Assign(driverId, vehicleId, null, _dateTimeProvider.UtcNow);
        _repo.Tasks[task.Id] = task;

        // Configure current user as driver
        var driverUserId = Guid.NewGuid();
        _currentUser.UserId = driverUserId;
        _currentUser.Role = UserRole.Driver;
        _repo.UserToDriverMap[driverUserId] = driverId;

        // 2. Driver Acknowledges task (F5.2: Assigned -> Acknowledged)
        var ackResult = await _ackHandler.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None);
        ackResult.IsSuccess.Should().BeTrue();
        task.Status.Should().Be(JobTaskStatus.Acknowledged);
        task.AcknowledgedAt.Should().NotBeNull();

        // 3. Driver Starts task (Acknowledged -> InProgress)
        var startResult = await _startHandler.Handle(new StartJobTaskCommand(task.Id, StartOdometerKm: 120500m), CancellationToken.None);
        startResult.IsSuccess.Should().BeTrue();
        task.Status.Should().Be(JobTaskStatus.InProgress);
        task.StartedAt.Should().NotBeNull();
        task.StartOdometerKm?.Value.Should().Be(120500m);

        // 4. Driver Completes task (InProgress -> Completed)
        var completeResult = await _completeHandler.Handle(new CompleteJobTaskCommand(task.Id, ActualDistanceKm: 45m, EndOdometerKm: 120545m), CancellationToken.None);
        completeResult.IsSuccess.Should().BeTrue();
        task.Status.Should().Be(JobTaskStatus.Completed);
        task.CompletedAt.Should().NotBeNull();
        task.ActualDistanceKm?.Value.Should().Be(45m);
        task.EndOdometerKm?.Value.Should().Be(120545m);
        task.EffectiveDistanceKm?.Value.Should().Be(45m);
    }

    [Fact]
    public async Task Acknowledge_DraftTask_Returns422InvalidTransition()
    {
        // Arrange: Task is in Draft status (cannot jump directly to Acknowledged)
        var task = new JobTask(Guid.NewGuid(), "TSK-DRAFT", "Draft Task", Guid.NewGuid(), _dateTimeProvider.UtcNow, Guid.NewGuid());
        _repo.Tasks[task.Id] = task;

        // Act
        var result = await _ackHandler.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("invalid_task_transition");
    }

    [Fact]
    public async Task Acknowledge_DifferentDriver_Returns403Forbidden()
    {
        // Arrange
        var assignedDriverId = Guid.NewGuid();
        var otherDriverId = Guid.NewGuid();
        var task = new JobTask(Guid.NewGuid(), "TSK-OTHER", "Task", Guid.NewGuid(), _dateTimeProvider.UtcNow, Guid.NewGuid());
        task.Assign(assignedDriverId, Guid.NewGuid(), null, _dateTimeProvider.UtcNow);
        _repo.Tasks[task.Id] = task;

        // Current user is another driver
        var otherUserId = Guid.NewGuid();
        _currentUser.UserId = otherUserId;
        _currentUser.Role = UserRole.Driver;
        _repo.UserToDriverMap[otherUserId] = otherDriverId;

        // Act
        var result = await _ackHandler.Handle(new AcknowledgeJobTaskCommand(task.Id), CancellationToken.None);

        // Assert: 403 Forbidden
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }

    [Fact]
    public async Task Start_DraftTask_Returns422InvalidTransition()
    {
        // Arrange: Draft task cannot start
        var task = new JobTask(Guid.NewGuid(), "TSK-DRAFT-START", "Task", Guid.NewGuid(), _dateTimeProvider.UtcNow, Guid.NewGuid());
        _repo.Tasks[task.Id] = task;

        // Act
        var result = await _startHandler.Handle(new StartJobTaskCommand(task.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("invalid_task_transition");
    }

    [Fact]
    public async Task Complete_DraftTask_Returns422InvalidTransition()
    {
        // Arrange
        var task = new JobTask(Guid.NewGuid(), "TSK-DRAFT-COMP", "Task", Guid.NewGuid(), _dateTimeProvider.UtcNow, Guid.NewGuid());
        _repo.Tasks[task.Id] = task;

        // Act
        var result = await _completeHandler.Handle(new CompleteJobTaskCommand(task.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("invalid_task_transition");
    }

    [Fact]
    public async Task Cancel_AssignedTask_Succeeds()
    {
        // Arrange
        var task = new JobTask(Guid.NewGuid(), "TSK-CANCEL", "Task", Guid.NewGuid(), _dateTimeProvider.UtcNow, Guid.NewGuid());
        task.Assign(Guid.NewGuid(), Guid.NewGuid(), null, _dateTimeProvider.UtcNow);
        _repo.Tasks[task.Id] = task;

        // Act
        var result = await _cancelHandler.Handle(new CancelJobTaskCommand(task.Id, "Customer cancelled booking"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Status.Should().Be(JobTaskStatus.Cancelled);
        task.CancellationReason.Should().Be("Customer cancelled booking");
    }

    [Fact]
    public async Task Cancel_CompletedTask_Returns422InvalidTransition()
    {
        // Arrange: Completed task cannot be cancelled
        var task = new JobTask(Guid.NewGuid(), "TSK-COMP-CANC", "Task", Guid.NewGuid(), _dateTimeProvider.UtcNow, Guid.NewGuid());
        task.Assign(Guid.NewGuid(), Guid.NewGuid(), null, _dateTimeProvider.UtcNow);
        task.Acknowledge(_dateTimeProvider.UtcNow);
        task.Start(_dateTimeProvider.UtcNow);
        task.Complete(_dateTimeProvider.UtcNow);
        _repo.Tasks[task.Id] = task;

        // Act
        var result = await _cancelHandler.Handle(new CancelJobTaskCommand(task.Id, "Too late"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("invalid_task_transition");
    }
}
