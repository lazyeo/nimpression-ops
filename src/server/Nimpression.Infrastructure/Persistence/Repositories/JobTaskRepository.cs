using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;

namespace Nimpression.Infrastructure.Persistence.Repositories;

/// <summary>
/// 派发任务仓储实现。
/// </summary>
public sealed class JobTaskRepository(AppDbContext dbContext) : IJobTaskRepository
{
    public async Task<JobTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.JobTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<JobTask?> GetByRefAsync(string @ref, CancellationToken cancellationToken = default)
    {
        var normalized = @ref.Trim().ToUpperInvariant();
        return await dbContext.JobTasks.FirstOrDefaultAsync(t => t.Ref == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByRefAsync(string @ref, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = @ref.Trim().ToUpperInvariant();
        return await dbContext.JobTasks.AnyAsync(
            t => t.Ref == normalized && (!excludeId.HasValue || t.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);
    }

    public async Task<Guid?> GetDriverIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var driver = await dbContext.Drivers.AsNoTracking()
            .Where(d => d.UserId == userId)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return driver;
    }

    public async Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Vehicles.AnyAsync(v => v.Id == vehicleId, cancellationToken);
    }

    public async Task<bool> AreaExistsAsync(Guid areaId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Areas.AnyAsync(a => a.Id == areaId, cancellationToken);
    }

    public async Task<bool> IsDriverAssignedToAreaOnDateAsync(Guid driverId, Guid areaId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return await dbContext.AreaAssignments.AsNoTracking().AnyAsync(
            aa => aa.DriverId == driverId && aa.AreaId == areaId && aa.EffectiveFrom <= date && (aa.EffectiveTo == null || aa.EffectiveTo >= date),
            cancellationToken);
    }

    public async Task AddJobTaskAsync(JobTask jobTask, CancellationToken cancellationToken = default)
    {
        await dbContext.JobTasks.AddAsync(jobTask, cancellationToken);
    }

    public void UpdateJobTask(JobTask jobTask)
    {
        dbContext.JobTasks.Update(jobTask);
    }

    public async Task<PagedResult<JobTaskSummaryDto>> GetJobTasksPagedAsync(JobTaskFilter filter, CancellationToken cancellationToken = default)
    {
        var query = from t in dbContext.JobTasks.AsNoTracking()
                    join a in dbContext.Areas.AsNoTracking() on t.AreaId equals a.Id
                    join d in dbContext.Drivers.AsNoTracking() on t.DriverId equals d.Id into dGroup
                    from d in dGroup.DefaultIfEmpty()
                    join u in dbContext.Users.AsNoTracking() on d.UserId equals u.Id into uGroup
                    from u in uGroup.DefaultIfEmpty()
                    join v in dbContext.Vehicles.AsNoTracking() on t.VehicleId equals v.Id into vGroup
                    from v in vGroup.DefaultIfEmpty()
                    select new
                    {
                        Task = t,
                        Area = a,
                        DriverName = u != null ? u.DisplayName : null,
                        VehicleRego = v != null ? v.Rego.Value : null
                    };

        if (filter.DriverId.HasValue)
        {
            query = query.Where(x => x.Task.DriverId == filter.DriverId.Value);
        }

        if (filter.VehicleId.HasValue)
        {
            query = query.Where(x => x.Task.VehicleId == filter.VehicleId.Value);
        }

        if (filter.AreaId.HasValue)
        {
            query = query.Where(x => x.Task.AreaId == filter.AreaId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Task.Status == filter.Status.Value);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(x => x.Task.ScheduledFor >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(x => x.Task.ScheduledFor <= filter.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Task.Title, $"%{search}%") ||
                                     EF.Functions.ILike(x.Task.Ref, $"%{search}%") ||
                                     (x.DriverName != null && EF.Functions.ILike(x.DriverName, $"%{search}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var rawItems = await query
            .OrderByDescending(x => x.Task.ScheduledFor)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = rawItems.Select(x => new JobTaskSummaryDto(
            x.Task.Id,
            x.Task.Ref,
            x.Task.Title,
            x.Task.AreaId,
            x.Area.Name,
            x.Area.Code,
            x.Task.DriverId,
            x.DriverName,
            x.Task.VehicleId,
            x.VehicleRego,
            x.Task.ScheduledFor,
            x.Task.Priority,
            x.Task.Status,
            x.Task.PlannedDistanceKm?.Value,
            x.Task.EffectiveDistanceKm?.Value)).ToList();

        return new PagedResult<JobTaskSummaryDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<JobTaskDetailDto?> GetJobTaskDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var raw = await (from t in dbContext.JobTasks.AsNoTracking()
                         join a in dbContext.Areas.AsNoTracking() on t.AreaId equals a.Id
                         join d in dbContext.Drivers.AsNoTracking() on t.DriverId equals d.Id into dGroup
                         from d in dGroup.DefaultIfEmpty()
                         join du in dbContext.Users.AsNoTracking() on d.UserId equals du.Id into duGroup
                         from du in duGroup.DefaultIfEmpty()
                         join v in dbContext.Vehicles.AsNoTracking() on t.VehicleId equals v.Id into vGroup
                         from v in vGroup.DefaultIfEmpty()
                         join cu in dbContext.Users.AsNoTracking() on t.CreatedByUserId equals cu.Id into cuGroup
                         from cu in cuGroup.DefaultIfEmpty()
                         where t.Id == id
                         select new
                         {
                             Task = t,
                             Area = a,
                             DriverName = du != null ? du.DisplayName : null,
                             VehicleRego = v != null ? v.Rego.Value : null,
                             CreatedByUserName = cu != null ? cu.DisplayName : null
                         }).FirstOrDefaultAsync(cancellationToken);

        if (raw is null)
        {
            return null;
        }

        return new JobTaskDetailDto(
            raw.Task.Id,
            raw.Task.Ref,
            raw.Task.Title,
            raw.Task.Description,
            raw.Task.AreaId,
            raw.Area.Name,
            raw.Area.Code,
            raw.Task.DriverId,
            raw.DriverName,
            raw.Task.VehicleId,
            raw.VehicleRego,
            raw.Task.ScheduledFor,
            raw.Task.Priority,
            raw.Task.Status,
            raw.Task.AcknowledgedAt,
            raw.Task.StartedAt,
            raw.Task.CompletedAt,
            raw.Task.CancelledAt,
            raw.Task.CancellationReason,
            raw.Task.CreatedByUserId,
            raw.CreatedByUserName,
            raw.Task.PlannedDistanceKm?.Value,
            raw.Task.ActualDistanceKm?.Value,
            raw.Task.StartOdometerKm?.Value,
            raw.Task.EndOdometerKm?.Value,
            raw.Task.EffectiveDistanceKm?.Value);
    }

    public async Task<List<JobTaskAlertDto>> GetUnacknowledgedTaskAlertsAsync(int thresholdMinutes, DateTimeOffset referenceTime, CancellationToken cancellationToken = default)
    {
        var cutoff = referenceTime.AddMinutes(-thresholdMinutes);

        var tasks = await (from t in dbContext.JobTasks.AsNoTracking()
                           join a in dbContext.Areas.AsNoTracking() on t.AreaId equals a.Id
                           join d in dbContext.Drivers.AsNoTracking() on t.DriverId equals d.Id into dGroup
                           from d in dGroup.DefaultIfEmpty()
                           join u in dbContext.Users.AsNoTracking() on d.UserId equals u.Id into uGroup
                           from u in uGroup.DefaultIfEmpty()
                           join v in dbContext.Vehicles.AsNoTracking() on t.VehicleId equals v.Id into vGroup
                           from v in vGroup.DefaultIfEmpty()
                           where t.Status == JobTaskStatus.Assigned && t.ScheduledFor <= cutoff
                           orderby t.ScheduledFor
                           select new
                           {
                               t.Id,
                               t.Ref,
                               t.Title,
                               DriverId = t.DriverId ?? Guid.Empty,
                               DriverName = u != null ? u.DisplayName : null,
                               t.VehicleId,
                               VehicleRego = v != null ? v.Rego.Value : null,
                               t.AreaId,
                               AreaName = a.Name,
                               t.ScheduledFor
                           }).ToListAsync(cancellationToken);

        return tasks.Select(x => new JobTaskAlertDto(
            x.Id,
            x.Ref,
            x.Title,
            x.DriverId,
            x.DriverName,
            x.VehicleId,
            x.VehicleRego,
            x.AreaId,
            x.AreaName,
            x.ScheduledFor,
            (int)(referenceTime - x.ScheduledFor).TotalMinutes)).ToList();
    }

    private static readonly JobTaskStatus[] ActiveStatuses = [JobTaskStatus.Assigned, JobTaskStatus.Acknowledged, JobTaskStatus.InProgress];

    public async Task<List<DriverTaskItemDto>> GetDriverTasksAsync(Guid driverId, JobTaskStatus? status = null, bool? activeOnly = null, CancellationToken cancellationToken = default)
    {
        var query = from t in dbContext.JobTasks.AsNoTracking()
                    join a in dbContext.Areas.AsNoTracking() on t.AreaId equals a.Id
                    join v in dbContext.Vehicles.AsNoTracking() on t.VehicleId equals v.Id into vGroup
                    from v in vGroup.DefaultIfEmpty()
                    where t.DriverId == driverId
                    select new
                    {
                        Task = t,
                        AreaName = a.Name,
                        VehicleRego = v != null ? v.Rego.Value : string.Empty
                    };

        if (activeOnly == true)
        {
            query = query.Where(x => ActiveStatuses.Contains(x.Task.Status));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Task.Status == status.Value);
        }

        var items = await query
            .OrderByDescending(x => x.Task.ScheduledFor)
            .ToListAsync(cancellationToken);

        return items.Select(x => new DriverTaskItemDto(
            x.Task.Id,
            x.Task.Ref,
            MapStatusToDriverStatus(x.Task.Status),
            $"{x.AreaName} Hub",
            !string.IsNullOrWhiteSpace(x.Task.Description) ? x.Task.Description : (!string.IsNullOrWhiteSpace(x.Task.Title) ? x.Task.Title : x.AreaName),
            x.Task.ScheduledFor,
            x.VehicleRego)).ToList();
    }

    public async Task<DashboardMetricsDto> GetDashboardMetricsAsync(CancellationToken cancellationToken = default)
    {
        var activeDispatches = await dbContext.JobTasks.AsNoTracking()
            .CountAsync(t => t.Status == JobTaskStatus.InProgress || t.Status == JobTaskStatus.Assigned || t.Status == JobTaskStatus.Acknowledged, cancellationToken);

        var onlineDrivers = await dbContext.ShiftEntries.AsNoTracking()
            .CountAsync(s => s.Status == ShiftStatus.Active, cancellationToken);

        var pendingIncidents = await dbContext.IncidentReports.AsNoTracking()
            .CountAsync(i => i.Status == "Reported" || i.Status == "Investigating", cancellationToken);

        var unresolvedFines = await dbContext.Fines.AsNoTracking()
            .CountAsync(f => f.Status == FineStatus.Submitted || f.Status == FineStatus.UnderReview || f.Status == FineStatus.Disputed, cancellationToken);

        return new DashboardMetricsDto(activeDispatches, onlineDrivers, pendingIncidents, unresolvedFines);
    }

    private static string MapStatusToDriverStatus(JobTaskStatus status) => status switch
    {
        JobTaskStatus.Draft => "PENDING",
        JobTaskStatus.Assigned => "ASSIGNED",
        JobTaskStatus.Acknowledged => "ASSIGNED",
        JobTaskStatus.InProgress => "IN_PROGRESS",
        JobTaskStatus.Completed => "COMPLETED",
        JobTaskStatus.Cancelled => "CANCELLED",
        _ => "PENDING"
    };
}
