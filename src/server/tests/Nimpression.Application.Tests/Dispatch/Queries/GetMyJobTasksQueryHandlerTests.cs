using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Queries.GetMyJobTasks;
using Nimpression.Application.Tests.Dispatch.TestDoubles;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Dispatch.Queries;

public sealed class GetMyJobTasksQueryHandlerTests
{
    private readonly FakeJobTaskRepository _repository = new();
    private readonly Guid _driverUserId = Guid.NewGuid();
    private readonly Guid _driverId = Guid.NewGuid();
    private readonly Guid _otherDriverUserId = Guid.NewGuid();
    private readonly Guid _otherDriverId = Guid.NewGuid();

    public GetMyJobTasksQueryHandlerTests()
    {
        _repository.UserToDriverMap[_driverUserId] = _driverId;
        _repository.UserToDriverMap[_otherDriverUserId] = _otherDriverId;
    }

    [Fact]
    public async Task Handle_DriverCanOnlyRetrieveOwnTasks_NotOtherDriverTasks()
    {
        // Arrange: 2 tasks for Driver A, 1 task for Driver B
        var taskA1 = new JobTask(Guid.NewGuid(), "TSK-20260824-0001", "Delivery 1", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: _driverId, vehicleId: Guid.NewGuid());
        var taskA2 = new JobTask(Guid.NewGuid(), "TSK-20260824-0002", "Delivery 2", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: _driverId, vehicleId: Guid.NewGuid());
        var taskB1 = new JobTask(Guid.NewGuid(), "TSK-20260824-0003", "Other Delivery", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: _otherDriverId, vehicleId: Guid.NewGuid());

        _repository.Tasks[taskA1.Id] = taskA1;
        _repository.Tasks[taskA2.Id] = taskA2;
        _repository.Tasks[taskB1.Id] = taskB1;

        var currentUser = new FakeCurrentUser(_driverUserId, UserRole.Driver);
        var handler = new GetMyJobTasksQueryHandler(_repository, currentUser);

        // Act
        var result = await handler.Handle(new GetMyJobTasksQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.All(result.Value.Items, t => Assert.True(t.Id == taskA1.Id || t.Id == taskA2.Id));
        Assert.DoesNotContain(result.Value.Items, t => t.Id == taskB1.Id);
    }

    [Fact]
    public async Task Handle_NonDriverRole_ReturnsForbidden()
    {
        var currentUser = new FakeCurrentUser(Guid.NewGuid(), UserRole.Dispatcher);
        var handler = new GetMyJobTasksQueryHandler(_repository, currentUser);

        var result = await handler.Handle(new GetMyJobTasksQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task Handle_DriverWithoutProfile_ReturnsForbidden()
    {
        var unlinkedUserId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser(unlinkedUserId, UserRole.Driver);
        var handler = new GetMyJobTasksQueryHandler(_repository, currentUser);

        var result = await handler.Handle(new GetMyJobTasksQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task Handle_ActiveOnlyTrue_ReturnsOnlyAssignedAcknowledgedInProgressTasks()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        var areaId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var assignedTask = new JobTask(Guid.NewGuid(), "TSK-001", "Assigned Task", areaId, baseTime, creatorId, driverId: _driverId, vehicleId: vehicleId);

        var ackTask = new JobTask(Guid.NewGuid(), "TSK-002", "Ack Task", areaId, baseTime.AddHours(1), creatorId, driverId: _driverId, vehicleId: vehicleId);
        ackTask.Acknowledge(baseTime.AddMinutes(10));

        var inProgressTask = new JobTask(Guid.NewGuid(), "TSK-003", "InProgress Task", areaId, baseTime.AddHours(2), creatorId, driverId: _driverId, vehicleId: vehicleId);
        inProgressTask.Acknowledge(baseTime.AddMinutes(10));
        inProgressTask.Start(baseTime.AddMinutes(20), new Kilometres(100m));

        var completedTask = new JobTask(Guid.NewGuid(), "TSK-004", "Completed Task", areaId, baseTime.AddHours(3), creatorId, driverId: _driverId, vehicleId: vehicleId);
        completedTask.Acknowledge(baseTime.AddMinutes(10));
        completedTask.Start(baseTime.AddMinutes(20), new Kilometres(100m));
        completedTask.Complete(baseTime.AddMinutes(50), new Kilometres(30m), new Kilometres(130m));

        var cancelledTask = new JobTask(Guid.NewGuid(), "TSK-005", "Cancelled Task", areaId, baseTime.AddHours(4), creatorId, driverId: _driverId, vehicleId: vehicleId);
        cancelledTask.Cancel("Driver sick", baseTime.AddMinutes(5));

        _repository.Tasks[assignedTask.Id] = assignedTask;
        _repository.Tasks[ackTask.Id] = ackTask;
        _repository.Tasks[inProgressTask.Id] = inProgressTask;
        _repository.Tasks[completedTask.Id] = completedTask;
        _repository.Tasks[cancelledTask.Id] = cancelledTask;

        var currentUser = new FakeCurrentUser(_driverUserId, UserRole.Driver);
        var handler = new GetMyJobTasksQueryHandler(_repository, currentUser);

        // Act
        var result = await handler.Handle(new GetMyJobTasksQuery(ActiveOnly: true), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value.Items.Count);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Contains(result.Value.Items, t => t.Id == assignedTask.Id);
        Assert.Contains(result.Value.Items, t => t.Id == ackTask.Id);
        Assert.Contains(result.Value.Items, t => t.Id == inProgressTask.Id);
        Assert.DoesNotContain(result.Value.Items, t => t.Id == completedTask.Id);
        Assert.DoesNotContain(result.Value.Items, t => t.Id == cancelledTask.Id);
    }

    [Fact]
    public async Task Handle_ActiveOnlyFalse_ReturnsOnlyNonActiveCompletedCancelledTasks()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        var areaId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var assignedTask = new JobTask(Guid.NewGuid(), "TSK-001", "Assigned Task", areaId, baseTime, creatorId, driverId: _driverId, vehicleId: vehicleId);

        var completedTask = new JobTask(Guid.NewGuid(), "TSK-002", "Completed Task", areaId, baseTime.AddHours(2), creatorId, driverId: _driverId, vehicleId: vehicleId);
        completedTask.Acknowledge(baseTime.AddMinutes(10));
        completedTask.Start(baseTime.AddMinutes(20), new Kilometres(100m));
        completedTask.Complete(baseTime.AddMinutes(50), new Kilometres(30m), new Kilometres(130m));

        var cancelledTask = new JobTask(Guid.NewGuid(), "TSK-003", "Cancelled Task", areaId, baseTime.AddHours(3), creatorId, driverId: _driverId, vehicleId: vehicleId);
        cancelledTask.Cancel("Driver unavailable", baseTime.AddMinutes(5));

        _repository.Tasks[assignedTask.Id] = assignedTask;
        _repository.Tasks[completedTask.Id] = completedTask;
        _repository.Tasks[cancelledTask.Id] = cancelledTask;

        var currentUser = new FakeCurrentUser(_driverUserId, UserRole.Driver);
        var handler = new GetMyJobTasksQueryHandler(_repository, currentUser);

        // Act
        var result = await handler.Handle(new GetMyJobTasksQuery(ActiveOnly: false), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Contains(result.Value.Items, t => t.Id == completedTask.Id);
        Assert.Contains(result.Value.Items, t => t.Id == cancelledTask.Id);
        Assert.DoesNotContain(result.Value.Items, t => t.Id == assignedTask.Id);
    }

    [Fact]
    public async Task Handle_ServerPagination_ReturnsCorrectPageAndMetadata()
    {
        // Arrange: 7 completed tasks, page 2 of size 3
        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        var areaId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        for (var i = 1; i <= 7; i++)
        {
            var t = new JobTask(Guid.NewGuid(), $"TSK-00{i}", $"Completed {i}", areaId, baseTime.AddHours(i), creatorId, driverId: _driverId, vehicleId: vehicleId);
            t.Acknowledge(baseTime.AddHours(i).AddMinutes(10));
            t.Start(baseTime.AddHours(i).AddMinutes(20), new Kilometres(100m));
            t.Complete(baseTime.AddHours(i).AddMinutes(50), new Kilometres(30m), new Kilometres(130m));
            _repository.Tasks[t.Id] = t;
        }

        var currentUser = new FakeCurrentUser(_driverUserId, UserRole.Driver);
        var handler = new GetMyJobTasksQueryHandler(_repository, currentUser);

        // Act: Page 2, PageSize 3
        var result = await handler.Handle(new GetMyJobTasksQuery(ActiveOnly: false, Page: 2, PageSize: 3), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(7, result.Value.TotalCount);
        Assert.Equal(3, result.Value.Items.Count);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(3, result.Value.PageSize);
        Assert.Equal(3, result.Value.TotalPages);
        Assert.True(result.Value.HasPreviousPage);
        Assert.True(result.Value.HasNextPage);
    }

    [Fact]
    public async Task Handle_ActiveOnlyTrueWithStatusFilter_AppliesBothFilters()
    {
        // Arrange
        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
        var areaId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var assignedTask = new JobTask(Guid.NewGuid(), "TSK-001", "Assigned Task", areaId, baseTime, creatorId, driverId: _driverId, vehicleId: vehicleId);

        var inProgressTask = new JobTask(Guid.NewGuid(), "TSK-002", "InProgress Task", areaId, baseTime.AddHours(1), creatorId, driverId: _driverId, vehicleId: vehicleId);
        inProgressTask.Acknowledge(baseTime.AddMinutes(10));
        inProgressTask.Start(baseTime.AddMinutes(20), new Kilometres(100m));

        var completedTask = new JobTask(Guid.NewGuid(), "TSK-003", "Completed Task", areaId, baseTime.AddHours(2), creatorId, driverId: _driverId, vehicleId: vehicleId);
        completedTask.Acknowledge(baseTime.AddMinutes(10));
        completedTask.Start(baseTime.AddMinutes(20), new Kilometres(100m));
        completedTask.Complete(baseTime.AddMinutes(50), new Kilometres(30m), new Kilometres(130m));

        _repository.Tasks[assignedTask.Id] = assignedTask;
        _repository.Tasks[inProgressTask.Id] = inProgressTask;
        _repository.Tasks[completedTask.Id] = completedTask;

        var currentUser = new FakeCurrentUser(_driverUserId, UserRole.Driver);
        var handler = new GetMyJobTasksQueryHandler(_repository, currentUser);

        // Act 1: ActiveOnly=true AND Status=Assigned -> returns assignedTask
        var resultAssigned = await handler.Handle(new GetMyJobTasksQuery(Status: JobTaskStatus.Assigned, ActiveOnly: true), CancellationToken.None);
        Assert.True(resultAssigned.IsSuccess);
        Assert.Single(resultAssigned.Value!.Items);
        Assert.Equal(assignedTask.Id, resultAssigned.Value!.Items[0].Id);

        // Act 2: ActiveOnly=true AND Status=Completed -> returns empty
        var resultCompleted = await handler.Handle(new GetMyJobTasksQuery(Status: JobTaskStatus.Completed, ActiveOnly: true), CancellationToken.None);
        Assert.True(resultCompleted.IsSuccess);
        Assert.Empty(resultCompleted.Value!.Items);
    }
}
