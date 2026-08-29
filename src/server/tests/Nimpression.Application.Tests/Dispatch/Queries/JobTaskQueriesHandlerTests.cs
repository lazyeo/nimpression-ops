using FluentAssertions;
using Nimpression.Application.Features.Dispatch.DTOs;
using Nimpression.Application.Features.Dispatch.Queries.CheckAreaEligibility;
using Nimpression.Application.Features.Dispatch.Queries.GetJobTaskById;
using Nimpression.Application.Features.Dispatch.Queries.GetJobTasksList;
using Nimpression.Application.Features.Dispatch.Queries.GetUnacknowledgedTaskAlerts;
using Nimpression.Application.Tests.Areas.TestDoubles;
using Nimpression.Application.Tests.Dispatch.TestDoubles;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;
using Xunit;

namespace Nimpression.Application.Tests.Dispatch.Queries;

public sealed class JobTaskQueriesHandlerTests
{
    private readonly FakeJobTaskRepository _repo = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();

    [Fact]
    public async Task GetUnacknowledgedTaskAlerts_ReturnsTasksUnacknowledgedForOver30Minutes()
    {
        // Arrange (F5.5)
        var areaId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        // 任务 1: 指派时间为 40 分钟前（超过 30 分钟）
        var task1 = new JobTask(Guid.NewGuid(), "TSK-ALERT-01", "Urgent Run", areaId, _dateTimeProvider.UtcNow.AddMinutes(-40), Guid.NewGuid());
        task1.Assign(driverId, vehicleId, null, _dateTimeProvider.UtcNow.AddMinutes(-40));
        _repo.Tasks[task1.Id] = task1;

        // 任务 2: 指派时间为 10 分钟前（未到 30 分钟）
        var task2 = new JobTask(Guid.NewGuid(), "TSK-ALERT-02", "Recent Run", areaId, _dateTimeProvider.UtcNow.AddMinutes(-10), Guid.NewGuid());
        task2.Assign(driverId, vehicleId, null, _dateTimeProvider.UtcNow.AddMinutes(-10));
        _repo.Tasks[task2.Id] = task2;

        // 任务 3: 指派时间为 50 分钟前，但司机已确认（非 Assigned 状态）
        var task3 = new JobTask(Guid.NewGuid(), "TSK-ALERT-03", "Acked Run", areaId, _dateTimeProvider.UtcNow.AddMinutes(-50), Guid.NewGuid());
        task3.Assign(driverId, vehicleId, null, _dateTimeProvider.UtcNow.AddMinutes(-50));
        task3.Acknowledge(_dateTimeProvider.UtcNow.AddMinutes(-45));
        _repo.Tasks[task3.Id] = task3;

        var handler = new GetUnacknowledgedTaskAlertsQueryHandler(_repo, _dateTimeProvider);

        // Act
        var result = await handler.Handle(new GetUnacknowledgedTaskAlertsQuery(ThresholdMinutes: 30), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var alert = result.Value.Single();
        alert.TaskId.Should().Be(task1.Id);
        alert.Ref.Should().Be("TSK-ALERT-01");
        alert.MinutesUnacknowledged.Should().BeGreaterOrEqualTo(30);
    }

    [Fact]
    public async Task CheckAreaEligibility_WhenAssigned_ReturnsNoWarning()
    {
        // Arrange (F4.3)
        var driverId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 25);
        _repo.DriverAreaAssignments.Add((driverId, areaId, date));

        var handler = new CheckAreaEligibilityQueryHandler(_repo);

        // Act
        var result = await handler.Handle(new CheckAreaEligibilityQuery(driverId, areaId, date), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAssignedToArea.Should().BeTrue();
        result.Value.RequiresWarning.Should().BeFalse();
        result.Value.WarningMessage.Should().BeNull();
    }

    [Fact]
    public async Task CheckAreaEligibility_WhenNotAssigned_ReturnsWarning()
    {
        // Arrange (F4.3)
        var driverId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 25);
        // Not in assignments

        var handler = new CheckAreaEligibilityQueryHandler(_repo);

        // Act
        var result = await handler.Handle(new CheckAreaEligibilityQuery(driverId, areaId, date), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAssignedToArea.Should().BeFalse();
        result.Value.RequiresWarning.Should().BeTrue();
        result.Value.WarningMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetJobTaskById_ReturnsTaskDetails()
    {
        // Arrange
        var task = new JobTask(Guid.NewGuid(), "TSK-DETAIL-01", "Detail Run", Guid.NewGuid(), _dateTimeProvider.UtcNow, Guid.NewGuid(), "Description here");
        _repo.Tasks[task.Id] = task;

        var handler = new GetJobTaskByIdQueryHandler(_repo);

        // Act
        var result = await handler.Handle(new GetJobTaskByIdQuery(task.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Detail Run");
        result.Value.Ref.Should().Be("TSK-DETAIL-01");
        result.Value.Description.Should().Be("Description here");
    }
}
