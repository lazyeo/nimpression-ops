using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Tests.Dispatch.TestDoubles;

public sealed class FakeJobTaskRepository : IJobTaskRepository
{
    public Dictionary<Guid, JobTask> Tasks { get; } = [];
    public HashSet<Guid> ExistingDriverIds { get; } = [];
    public Dictionary<Guid, Guid> UserToDriverMap { get; } = [];
    public HashSet<Guid> ExistingVehicleIds { get; } = [];
    public HashSet<Guid> ExistingAreaIds { get; } = [];
    public HashSet<(Guid DriverId, Guid AreaId, DateOnly Date)> DriverAreaAssignments { get; } = [];

    public Task<JobTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Tasks.TryGetValue(id, out var task);
        return Task.FromResult(task);
    }

    public Task<JobTask?> GetByRefAsync(string @ref, CancellationToken cancellationToken = default)
    {
        var task = Tasks.Values.FirstOrDefault(t => string.Equals(t.Ref, @ref.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(task);
    }

    public Task<bool> ExistsByRefAsync(string @ref, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var exists = Tasks.Values.Any(t => string.Equals(t.Ref, @ref.Trim(), StringComparison.OrdinalIgnoreCase) && (!excludeId.HasValue || t.Id != excludeId.Value));
        return Task.FromResult(exists);
    }

    public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingDriverIds.Contains(driverId));
    }

    public Task<Guid?> GetDriverIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        UserToDriverMap.TryGetValue(userId, out var driverId);
        return Task.FromResult(driverId == Guid.Empty ? (Guid?)null : driverId);
    }

    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingVehicleIds.Contains(vehicleId));
    }

    public Task<bool> AreaExistsAsync(Guid areaId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingAreaIds.Contains(areaId));
    }

    public Task<bool> IsDriverAssignedToAreaOnDateAsync(Guid driverId, Guid areaId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DriverAreaAssignments.Contains((driverId, areaId, date)));
    }

    public Task AddJobTaskAsync(JobTask jobTask, CancellationToken cancellationToken = default)
    {
        Tasks[jobTask.Id] = jobTask;
        return Task.CompletedTask;
    }

    public void UpdateJobTask(JobTask jobTask)
    {
        Tasks[jobTask.Id] = jobTask;
    }

    public Task<PagedResult<JobTaskSummaryDto>> GetJobTasksPagedAsync(JobTaskFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Tasks.Values.AsEnumerable();

        if (filter.DriverId.HasValue) query = query.Where(t => t.DriverId == filter.DriverId.Value);
        if (filter.VehicleId.HasValue) query = query.Where(t => t.VehicleId == filter.VehicleId.Value);
        if (filter.AreaId.HasValue) query = query.Where(t => t.AreaId == filter.AreaId.Value);
        if (filter.Status.HasValue) query = query.Where(t => t.Status == filter.Status.Value);
        if (filter.From.HasValue) query = query.Where(t => t.ScheduledFor >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(t => t.ScheduledFor <= filter.To.Value);
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            query = query.Where(t => t.Title.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                     t.Ref.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        var total = query.Count();
        var items = query
            .OrderByDescending(t => t.ScheduledFor)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(t => new JobTaskSummaryDto(
                t.Id,
                t.Ref,
                t.Title,
                t.AreaId,
                "Area Name",
                "AREA-CODE",
                t.DriverId,
                "Driver Name",
                t.VehicleId,
                "VEH-01",
                t.ScheduledFor,
                t.Priority,
                t.Status,
                t.PlannedDistanceKm?.Value,
                t.EffectiveDistanceKm?.Value))
            .ToList();

        return Task.FromResult(new PagedResult<JobTaskSummaryDto>(items, total, filter.Page, filter.PageSize));
    }

    public Task<JobTaskDetailDto?> GetJobTaskDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!Tasks.TryGetValue(id, out var t))
        {
            return Task.FromResult<JobTaskDetailDto?>(null);
        }

        var dto = new JobTaskDetailDto(
            t.Id,
            t.Ref,
            t.Title,
            t.Description,
            t.AreaId,
            "Area Name",
            "AREA-CODE",
            t.DriverId,
            "Driver Name",
            t.VehicleId,
            "VEH-01",
            t.ScheduledFor,
            t.Priority,
            t.Status,
            t.AcknowledgedAt,
            t.StartedAt,
            t.CompletedAt,
            t.CancelledAt,
            t.CancellationReason,
            t.CreatedByUserId,
            "Dispatcher User",
            t.PlannedDistanceKm?.Value,
            t.ActualDistanceKm?.Value,
            t.StartOdometerKm?.Value,
            t.EndOdometerKm?.Value,
            t.EffectiveDistanceKm?.Value);

        return Task.FromResult<JobTaskDetailDto?>(dto);
    }

    public Task<List<JobTaskAlertDto>> GetUnacknowledgedTaskAlertsAsync(int thresholdMinutes, DateTimeOffset referenceTime, CancellationToken cancellationToken = default)
    {
        var cutoff = referenceTime.AddMinutes(-thresholdMinutes);

        var list = Tasks.Values
            .Where(t => t.Status == JobTaskStatus.Assigned && t.ScheduledFor <= cutoff)
            .Select(t => new JobTaskAlertDto(
                t.Id,
                t.Ref,
                t.Title,
                t.DriverId ?? Guid.Empty,
                "Driver Name",
                t.VehicleId,
                "VEH-01",
                t.AreaId,
                "Area Name",
                t.ScheduledFor,
                (int)(referenceTime - t.ScheduledFor).TotalMinutes))
            .ToList();

        return Task.FromResult(list);
    }

    private static readonly JobTaskStatus[] ActiveStatuses = [JobTaskStatus.Assigned, JobTaskStatus.Acknowledged, JobTaskStatus.InProgress];

    public Task<List<DriverTaskItemDto>> GetDriverTasksAsync(Guid driverId, JobTaskStatus? status = null, bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var query = Tasks.Values.Where(t => t.DriverId == driverId);
        if (activeOnly == true)
        {
            query = query.Where(t => ActiveStatuses.Contains(t.Status));
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        var list = query
            .OrderByDescending(t => t.ScheduledFor)
            .Select(t => new DriverTaskItemDto(
                t.Id,
                t.Ref,
                t.Status switch
                {
                    JobTaskStatus.Draft => "PENDING",
                    JobTaskStatus.Assigned => "ASSIGNED",
                    JobTaskStatus.Acknowledged => "ASSIGNED",
                    JobTaskStatus.InProgress => "IN_PROGRESS",
                    JobTaskStatus.Completed => "COMPLETED",
                    JobTaskStatus.Cancelled => "CANCELLED",
                    _ => "PENDING"
                },
                "Area Name Hub",
                !string.IsNullOrWhiteSpace(t.Description) ? t.Description : (!string.IsNullOrWhiteSpace(t.Title) ? t.Title : "Area Name"),
                t.ScheduledFor,
                "VEH-01"))
            .ToList();

        return Task.FromResult(list);
    }

    public Task<DashboardMetricsDto> GetDashboardMetricsAsync(CancellationToken cancellationToken = default)
    {
        var activeDispatches = Tasks.Values.Count(t => t.Status == JobTaskStatus.InProgress || t.Status == JobTaskStatus.Assigned || t.Status == JobTaskStatus.Acknowledged);
        return Task.FromResult(new DashboardMetricsDto(activeDispatches, 10, 0, 1));
    }
}

public sealed class FakeAuditSink : IAuditSink
{
    public List<(string EntityType, Guid? EntityId, string Action, string? BeforeJson, string? AfterJson)> RecordedAudits { get; } = [];

    public Task RecordAsync(
        string entityType,
        Guid? entityId,
        string action,
        string? beforeJson = null,
        string? afterJson = null,
        CancellationToken cancellationToken = default)
    {
        RecordedAudits.Add((entityType, entityId, action, beforeJson, afterJson));
        return Task.CompletedTask;
    }
}

public sealed class FakeCurrentUser(
    Guid? userId = null,
    UserRole? role = UserRole.Dispatcher,
    string? ipAddress = "127.0.0.1",
    string? userAgent = "TestAgent") : ICurrentUser
{
    public Guid? UserId { get; set; } = userId ?? Guid.NewGuid();
    public UserRole? Role { get; set; } = role;
    public string? IpAddress { get; set; } = ipAddress;
    public string? UserAgent { get; set; } = userAgent;
    public bool IsAuthenticated => UserId.HasValue;
}
