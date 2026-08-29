using Nimpression.Application.Features.Incidents.DTOs;
using Nimpression.Domain.Entities.Compliance;

namespace Nimpression.Application.Features.Incidents.Abstractions;

/// <summary>
/// 事故报告仓储接口契约（F9 事故）。
/// </summary>
public interface IIncidentRepository
{
    Task<IncidentReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(IncidentReport report, CancellationToken cancellationToken = default);

    Task<PagedResult<IncidentReportDto>> GetIncidentsListAsync(IncidentFilter filter, CancellationToken cancellationToken = default);

    Task<IncidentReportDetailDto?> GetIncidentDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid?> GetDriverIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}
