using Nimpression.Application.Features.Dispatch.DTOs;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Dispatch.Abstractions;

/// <summary>
/// 派发任务仓储抽象契约。
/// </summary>
public interface IJobTaskRepository
{
    Task<JobTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<JobTask?> GetByRefAsync(string @ref, CancellationToken cancellationToken = default);

    Task<bool> ExistsByRefAsync(string @ref, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<Guid?> GetDriverIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<bool> AreaExistsAsync(Guid areaId, CancellationToken cancellationToken = default);

    Task<bool> IsDriverAssignedToAreaOnDateAsync(Guid driverId, Guid areaId, DateOnly date, CancellationToken cancellationToken = default);

    Task AddJobTaskAsync(JobTask jobTask, CancellationToken cancellationToken = default);

    void UpdateJobTask(JobTask jobTask);

    Task<PagedResult<JobTaskSummaryDto>> GetJobTasksPagedAsync(JobTaskFilter filter, CancellationToken cancellationToken = default);

    Task<JobTaskDetailDto?> GetJobTaskDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<JobTaskAlertDto>> GetUnacknowledgedTaskAlertsAsync(int thresholdMinutes, DateTimeOffset referenceTime, CancellationToken cancellationToken = default);

    Task<List<DriverTaskItemDto>> GetDriverTasksAsync(Guid driverId, JobTaskStatus? status = null, CancellationToken cancellationToken = default);

    Task<DashboardMetricsDto> GetDashboardMetricsAsync(CancellationToken cancellationToken = default);
}
