using Nimpression.Application.Features.Fines.DTOs;
using Nimpression.Domain.Entities.Compliance;

namespace Nimpression.Application.Features.Fines.Abstractions;

/// <summary>
/// 交通罚单仓储接口契约（F8 罚单）。
/// </summary>
public interface IFineRepository
{
    Task<Fine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Fine fine, CancellationToken cancellationToken = default);

    Task<PagedResult<FineDto>> GetFinesListAsync(FineFilter filter, CancellationToken cancellationToken = default);

    Task<FineDetailDto?> GetFineDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid?> GetDriverIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default);
}
