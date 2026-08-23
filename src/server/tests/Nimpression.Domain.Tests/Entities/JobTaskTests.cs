using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class JobTaskTests
{
    private static JobTask CreateTaskInState(JobTaskStatus state, Guid? driverId = null, Guid? vehicleId = null)
    {
        var driver = driverId ?? Guid.NewGuid();
        var vehicle = vehicleId ?? Guid.NewGuid();
        var area = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var task = new JobTask(Guid.NewGuid(), "TSK-001", "Delivery 1", area, now, admin);

        if (state == JobTaskStatus.Draft)
        {
            return task;
        }

        task.Assign(driver, vehicle, now);
        if (state == JobTaskStatus.Assigned)
        {
            return task;
        }

        task.Acknowledge(now.AddMinutes(5));
        if (state == JobTaskStatus.Acknowledged)
        {
            return task;
        }

        task.Start(now.AddMinutes(10), new Kilometres(10000m));
        if (state == JobTaskStatus.InProgress)
        {
            return task;
        }

        if (state == JobTaskStatus.Completed)
        {
            task.Complete(now.AddHours(2), endOdometerKm: new Kilometres(10050m));
            return task;
        }

        if (state == JobTaskStatus.Cancelled)
        {
            task.Cancel("Customer cancelled", now.AddMinutes(15));
            return task;
        }

        throw new ArgumentOutOfRangeException(nameof(state), state, null);
    }

    [Fact]
    public void JobTask_full_happy_path_emits_events_and_calculates_distance()
    {
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var task = new JobTask(
            Guid.NewGuid(),
            "TSK-HAPPY",
            "Urgent Delivery",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "Deliver parcel",
            TaskPriority.Urgent,
            new Kilometres(35m));

        Assert.Equal(JobTaskStatus.Draft, task.Status);
        Assert.Equal("TSK-HAPPY", task.Ref);
        Assert.Equal(new Kilometres(35m), task.EffectiveDistanceKm);

        // Assign
        var assignTime = DateTimeOffset.UtcNow;
        task.Assign(driverId, vehicleId, assignedAt: assignTime);
        Assert.Equal(JobTaskStatus.Assigned, task.Status);
        var assignEvent = Assert.IsType<JobTaskAssigned>(Assert.Single(task.DomainEvents));
        Assert.Equal(task.Id, assignEvent.JobTaskId);
        Assert.Equal(driverId, assignEvent.DriverId);
        Assert.Equal(vehicleId, assignEvent.VehicleId);
        task.ClearDomainEvents();

        // Acknowledge
        var ackTime = assignTime.AddMinutes(5);
        task.Acknowledge(ackTime);
        Assert.Equal(JobTaskStatus.Acknowledged, task.Status);
        Assert.Equal(ackTime, task.AcknowledgedAt);
        var ackEvent = Assert.IsType<JobTaskAcknowledged>(Assert.Single(task.DomainEvents));
        Assert.Equal(driverId, ackEvent.DriverId);
        task.ClearDomainEvents();

        // Start
        var startTime = ackTime.AddMinutes(10);
        task.Start(startTime, new Kilometres(50000m));
        Assert.Equal(JobTaskStatus.InProgress, task.Status);
        Assert.Equal(startTime, task.StartedAt);
        Assert.Equal(new Kilometres(50000m), task.StartOdometerKm);

        // Complete
        var compTime = startTime.AddHours(1);
        task.Complete(compTime, endOdometerKm: new Kilometres(50042.5m));
        Assert.Equal(JobTaskStatus.Completed, task.Status);
        Assert.Equal(compTime, task.CompletedAt);
        Assert.Equal(new Kilometres(42.5m), task.ActualDistanceKm);
        Assert.Equal(new Kilometres(42.5m), task.EffectiveDistanceKm);
        var compEvent = Assert.IsType<JobTaskCompleted>(Assert.Single(task.DomainEvents));
        Assert.Equal(new Kilometres(42.5m), compEvent.DistanceKm);
    }

    [Theory]
    [InlineData(JobTaskStatus.Draft)]
    [InlineData(JobTaskStatus.Assigned)]
    [InlineData(JobTaskStatus.Acknowledged)]
    [InlineData(JobTaskStatus.InProgress)]
    public void JobTask_can_be_cancelled_from_non_terminal_states(JobTaskStatus initialState)
    {
        var task = CreateTaskInState(initialState);
        var cancelTime = DateTimeOffset.UtcNow;

        task.Cancel("Reason", cancelTime);
        Assert.Equal(JobTaskStatus.Cancelled, task.Status);
        Assert.Equal(cancelTime, task.CancelledAt);
        Assert.Equal("Reason", task.CancellationReason);
    }

    [Theory]
    // From Draft
    [InlineData(JobTaskStatus.Draft, JobTaskStatus.Acknowledged)]
    [InlineData(JobTaskStatus.Draft, JobTaskStatus.InProgress)]
    [InlineData(JobTaskStatus.Draft, JobTaskStatus.Completed)]
    // From Assigned
    [InlineData(JobTaskStatus.Assigned, JobTaskStatus.InProgress)]
    [InlineData(JobTaskStatus.Assigned, JobTaskStatus.Completed)]
    // From Acknowledged
    [InlineData(JobTaskStatus.Acknowledged, JobTaskStatus.Draft)]
    [InlineData(JobTaskStatus.Acknowledged, JobTaskStatus.Assigned)]
    [InlineData(JobTaskStatus.Acknowledged, JobTaskStatus.Completed)]
    // From InProgress
    [InlineData(JobTaskStatus.InProgress, JobTaskStatus.Draft)]
    [InlineData(JobTaskStatus.InProgress, JobTaskStatus.Assigned)]
    [InlineData(JobTaskStatus.InProgress, JobTaskStatus.Acknowledged)]
    // From Completed (Terminal)
    [InlineData(JobTaskStatus.Completed, JobTaskStatus.Draft)]
    [InlineData(JobTaskStatus.Completed, JobTaskStatus.Assigned)]
    [InlineData(JobTaskStatus.Completed, JobTaskStatus.Acknowledged)]
    [InlineData(JobTaskStatus.Completed, JobTaskStatus.InProgress)]
    [InlineData(JobTaskStatus.Completed, JobTaskStatus.Cancelled)]
    // From Cancelled (Terminal)
    [InlineData(JobTaskStatus.Cancelled, JobTaskStatus.Draft)]
    [InlineData(JobTaskStatus.Cancelled, JobTaskStatus.Assigned)]
    [InlineData(JobTaskStatus.Cancelled, JobTaskStatus.Acknowledged)]
    [InlineData(JobTaskStatus.Cancelled, JobTaskStatus.InProgress)]
    [InlineData(JobTaskStatus.Cancelled, JobTaskStatus.Completed)]
    public void JobTask_invalid_transitions_throw_InvalidJobTaskTransitionException(
        JobTaskStatus fromState, JobTaskStatus toState)
    {
        var task = CreateTaskInState(fromState);
        var now = DateTimeOffset.UtcNow;

        var ex = Assert.Throws<InvalidJobTaskTransitionException>(() =>
        {
            switch (toState)
            {
                case JobTaskStatus.Assigned:
                    task.Assign(Guid.NewGuid(), Guid.NewGuid(), now);
                    break;
                case JobTaskStatus.Acknowledged:
                    task.Acknowledge(now);
                    break;
                case JobTaskStatus.InProgress:
                    task.Start(now);
                    break;
                case JobTaskStatus.Completed:
                    task.Complete(now);
                    break;
                case JobTaskStatus.Cancelled:
                    task.Cancel("Cancel test", now);
                    break;
                default:
                    throw new InvalidJobTaskTransitionException(fromState, toState);
            }
        });

        Assert.Equal(fromState, ex.From);
        Assert.Equal(toState, ex.To);
    }

    [Fact]
    public void JobTask_odometer_validation_guards()
    {
        var task = CreateTaskInState(JobTaskStatus.Acknowledged);
        task.Start(DateTimeOffset.UtcNow, new Kilometres(10000m));

        // End < Start
        Assert.Throws<DomainValidationException>(() =>
            task.Complete(DateTimeOffset.UtcNow, endOdometerKm: new Kilometres(9999m)));

        // Difference > 1000 km
        Assert.Throws<DomainValidationException>(() =>
            task.Complete(DateTimeOffset.UtcNow, endOdometerKm: new Kilometres(11001m)));
    }

    [Fact]
    public void JobTask_distance_fallback_hierarchy()
    {
        var planned = new Kilometres(25m);
        var task = new JobTask(Guid.NewGuid(), "T-1", "Title", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), plannedDistanceKm: planned);
        Assert.Equal(planned, task.EffectiveDistanceKm);

        task.Assign(Guid.NewGuid(), Guid.NewGuid());
        task.Acknowledge(DateTimeOffset.UtcNow);
        task.Start(DateTimeOffset.UtcNow);

        var actual = new Kilometres(30m);
        task.Complete(DateTimeOffset.UtcNow, actualDistanceKm: actual);
        Assert.Equal(actual, task.EffectiveDistanceKm);
    }
}
