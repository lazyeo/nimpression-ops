using Nimpression.Application.Features.Areas.DTOs;
using Nimpression.Domain.Entities.Area;

namespace Nimpression.Application.Features.Areas.Abstractions;

/// <summary>
/// 区域仓储抽象契约。
/// </summary>
public interface IAreaRepository
{
    Task<Area?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Area?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveAssignmentsAsync(Guid areaId, DateOnly referenceDate, CancellationToken cancellationToken = default);

    Task AddAreaAsync(Area area, CancellationToken cancellationToken = default);

    void UpdateArea(Area area);

    void DeleteArea(Area area);

    Task<PagedResult<AreaDto>> GetAreasPagedAsync(AreaFilter filter, CancellationToken cancellationToken = default);

    Task<AreaDetailDto?> GetAreaDetailByIdAsync(Guid id, DateOnly referenceDate, CancellationToken cancellationToken = default);

    Task<List<AreaAssignment>> GetAssignmentsForDriverAndAreaAsync(Guid driverId, Guid areaId, CancellationToken cancellationToken = default);

    Task<AreaAssignment?> GetAssignmentByIdAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    Task AddAssignmentAsync(AreaAssignment assignment, CancellationToken cancellationToken = default);

    void DeleteAssignment(AreaAssignment assignment);

    Task<List<AreaAssignmentDto>> GetAreaAssignmentsAsync(Guid? areaId, Guid? driverId, DateOnly referenceDate, CancellationToken cancellationToken = default);

    Task<bool> IsDriverAssignedToAreaOnDateAsync(Guid driverId, Guid areaId, DateOnly date, CancellationToken cancellationToken = default);
}
