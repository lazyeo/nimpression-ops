using Nimpression.Application.Features.Vehicles.DTOs;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Vehicles.Abstractions;

/// <summary>
/// 车辆数据仓储契约。封装车辆、分派与里程读数的持久化与投影查询。
/// </summary>
public interface IVehicleRepository
{
    // 车辆 (Vehicles)
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByRegoAsync(Rego rego, CancellationToken cancellationToken = default);
    Task<bool> ExistsByRegoAsync(Rego rego, CancellationToken cancellationToken = default);
    Task<PagedResult<VehicleSummaryDto>> GetVehiclesPagedAsync(VehicleFilter filter, CancellationToken cancellationToken = default);
    Task<VehicleDetailDto?> GetVehicleDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    void UpdateVehicle(Vehicle vehicle);

    // 司机与分派 (Drivers & Assignments)
    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<VehicleAssignment?> GetAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VehicleAssignment?> GetActiveAssignmentByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleAssignmentDto>> GetAssignmentsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task AddAssignmentAsync(VehicleAssignment assignment, CancellationToken cancellationToken = default);
    void UpdateAssignment(VehicleAssignment assignment);

    // 里程读数 (Odometer Readings)
    Task<IReadOnlyList<OdometerReadingDto>> GetOdometerReadingsByVehicleIdAsync(Guid vehicleId, int limit = 50, CancellationToken cancellationToken = default);
    Task AddOdometerReadingAsync(OdometerReading reading, CancellationToken cancellationToken = default);
}
