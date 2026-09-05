using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Queries.GetMyJobTasks;
using Nimpression.Application.Tests.Dispatch.TestDoubles;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;
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
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, t => Assert.True(t.Id == taskA1.Id || t.Id == taskA2.Id));
        Assert.DoesNotContain(result.Value, t => t.Id == taskB1.Id);
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
}
