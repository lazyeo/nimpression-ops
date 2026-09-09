using FluentAssertions;
using NSubstitute;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Queries.GetJobTaskById;
using Nimpression.Application.Tests.Dispatch.TestDoubles;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Tests.Dispatch.Queries;

public sealed class JobTaskDetailAuthorizationTests
{
    [Theory]
    [InlineData(JobTaskStatus.Assigned)]
    [InlineData(JobTaskStatus.Completed)]
    [InlineData(JobTaskStatus.Cancelled)]
    public async Task Driver_CanReadOwnActiveAndHistoricalDetails_ButNotAnotherDrivers(JobTaskStatus status)
    {
        var repository = new FakeJobTaskRepository();
        var userId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        repository.UserToDriverMap[userId] = driverId;
        var now = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero);
        var task = new JobTask(Guid.NewGuid(), "DETAIL-OWN", "Deliver chilled food", Guid.NewGuid(), now, Guid.NewGuid(), "12 refrigerated cartons");
        task.Assign(driverId, Guid.NewGuid(), assignedAt: now);
        if (status == JobTaskStatus.Completed)
        {
            task.Acknowledge(now);
            task.Start(now);
            task.Complete(now);
        }
        else if (status == JobTaskStatus.Cancelled)
        {
            task.Cancel("Customer rescheduled", now);
        }
        repository.Tasks[task.Id] = task;
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.Role.Returns(UserRole.Driver);
        var handler = new GetJobTaskByIdQueryHandler(repository, user);

        var own = await handler.Handle(new GetJobTaskByIdQuery(task.Id), CancellationToken.None);
        own.IsSuccess.Should().BeTrue();
        own.Value.Description.Should().Be("12 refrigerated cartons");
        own.Value.Status.Should().Be(status);

        repository.UserToDriverMap[userId] = Guid.NewGuid();
        var other = await handler.Handle(new GetJobTaskByIdQuery(task.Id), CancellationToken.None);
        other.IsSuccess.Should().BeFalse();
        other.Error!.Kind.Should().Be(ErrorKind.Forbidden);

        repository.UserToDriverMap.Remove(userId);
        var missingProfile = await handler.Handle(new GetJobTaskByIdQuery(task.Id), CancellationToken.None);
        missingProfile.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }
}
