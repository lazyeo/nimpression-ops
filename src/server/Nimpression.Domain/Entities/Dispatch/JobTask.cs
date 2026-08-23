using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Dispatch;

/// <summary>
/// 派发任务聚合根。集中管理严格的状态机跃迁、起止里程与计酬距离计算。
/// </summary>
public sealed class JobTask : AggregateRoot
{
    public string Ref { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid AreaId { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Guid? DriverId { get; private set; }
    public DateTimeOffset ScheduledFor { get; private set; }
    public TaskPriority Priority { get; private set; }
    public JobTaskStatus Status { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Kilometres? PlannedDistanceKm { get; private set; }
    public Kilometres? ActualDistanceKm { get; private set; }
    public Kilometres? StartOdometerKm { get; private set; }
    public Kilometres? EndOdometerKm { get; private set; }

    private JobTask()
    {
    }

    public JobTask(
        Guid id,
        string @ref,
        string title,
        Guid areaId,
        DateTimeOffset scheduledFor,
        Guid createdByUserId,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium,
        Kilometres? plannedDistanceKm = null,
        Guid? driverId = null,
        Guid? vehicleId = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(@ref))
        {
            throw new DomainValidationException("Task reference cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainValidationException("Task title cannot be empty.");
        }

        if (areaId == Guid.Empty)
        {
            throw new DomainValidationException("AreaId cannot be empty.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainValidationException("CreatedByUserId cannot be empty.");
        }

        Ref = @ref.Trim().ToUpperInvariant();
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        AreaId = areaId;
        ScheduledFor = scheduledFor;
        Priority = priority;
        Status = JobTaskStatus.Draft;
        CreatedByUserId = createdByUserId;
        PlannedDistanceKm = plannedDistanceKm;

        if (driverId.HasValue && vehicleId.HasValue)
        {
            Assign(driverId.Value, vehicleId.Value, scheduledFor);
        }
    }

    public void Assign(Guid driverId, Guid vehicleId, DateTimeOffset? scheduledFor = null, DateTimeOffset? assignedAt = null)
    {
        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("DriverId cannot be empty.");
        }

        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("VehicleId cannot be empty.");
        }

        if (Status != JobTaskStatus.Draft && Status != JobTaskStatus.Assigned)
        {
            throw new InvalidJobTaskTransitionException(Status, JobTaskStatus.Assigned);
        }

        DriverId = driverId;
        VehicleId = vehicleId;
        if (scheduledFor.HasValue)
        {
            ScheduledFor = scheduledFor.Value;
        }

        Status = JobTaskStatus.Assigned;
        AddDomainEvent(new JobTaskAssigned(Id, driverId, vehicleId, assignedAt ?? DateTimeOffset.UtcNow));
    }

    public void Acknowledge(DateTimeOffset acknowledgedAt)
    {
        if (Status != JobTaskStatus.Assigned)
        {
            throw new InvalidJobTaskTransitionException(Status, JobTaskStatus.Acknowledged);
        }

        Status = JobTaskStatus.Acknowledged;
        AcknowledgedAt = acknowledgedAt;
        AddDomainEvent(new JobTaskAcknowledged(Id, DriverId!.Value, acknowledgedAt));
    }

    public void Start(DateTimeOffset startedAt, Kilometres? startOdometerKm = null)
    {
        if (Status != JobTaskStatus.Acknowledged)
        {
            throw new InvalidJobTaskTransitionException(Status, JobTaskStatus.InProgress);
        }

        Status = JobTaskStatus.InProgress;
        StartedAt = startedAt;
        if (startOdometerKm.HasValue)
        {
            StartOdometerKm = startOdometerKm.Value;
        }
    }

    public void Complete(
        DateTimeOffset completedAt,
        Kilometres? actualDistanceKm = null,
        Kilometres? endOdometerKm = null)
    {
        if (Status != JobTaskStatus.InProgress)
        {
            throw new InvalidJobTaskTransitionException(Status, JobTaskStatus.Completed);
        }

        if (endOdometerKm.HasValue)
        {
            if (StartOdometerKm.HasValue)
            {
                if (endOdometerKm.Value < StartOdometerKm.Value)
                {
                    throw new DomainValidationException(
                        $"End odometer ({endOdometerKm.Value.Value} km) cannot be less than start odometer ({StartOdometerKm.Value.Value} km).");
                }

                var diff = endOdometerKm.Value - StartOdometerKm.Value;
                if (diff.Value > 1000m)
                {
                    throw new DomainValidationException(
                        $"Odometer distance difference ({diff.Value} km) cannot exceed 1000 km.");
                }
            }

            EndOdometerKm = endOdometerKm;
        }

        if (actualDistanceKm.HasValue)
        {
            ActualDistanceKm = actualDistanceKm;
        }
        else if (EndOdometerKm.HasValue && StartOdometerKm.HasValue)
        {
            ActualDistanceKm = EndOdometerKm.Value - StartOdometerKm.Value;
        }

        Status = JobTaskStatus.Completed;
        CompletedAt = completedAt;
        AddDomainEvent(new JobTaskCompleted(Id, DriverId!.Value, EffectiveDistanceKm, completedAt));
    }

    public void Cancel(string reason, DateTimeOffset cancelledAt)
    {
        if (Status == JobTaskStatus.Completed || Status == JobTaskStatus.Cancelled)
        {
            throw new InvalidJobTaskTransitionException(Status, JobTaskStatus.Cancelled);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Cancellation reason cannot be empty.");
        }

        Status = JobTaskStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancellationReason = reason.Trim();
    }

    public Kilometres? EffectiveDistanceKm
    {
        get
        {
            if (EndOdometerKm.HasValue && StartOdometerKm.HasValue && EndOdometerKm.Value >= StartOdometerKm.Value)
            {
                return EndOdometerKm.Value - StartOdometerKm.Value;
            }

            if (ActualDistanceKm.HasValue)
            {
                return ActualDistanceKm.Value;
            }

            return PlannedDistanceKm;
        }
    }
}
