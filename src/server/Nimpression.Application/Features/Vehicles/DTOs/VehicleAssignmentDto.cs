namespace Nimpression.Application.Features.Vehicles.DTOs;

/// <summary>
/// 车辆分派记录 DTO。
/// </summary>
public sealed record VehicleAssignmentDto(
    Guid Id,
    Guid VehicleId,
    string? VehicleRego,
    Guid DriverId,
    string? DriverName,
    string? DriverEmployeeNo,
    DateTimeOffset AssignedAt,
    DateTimeOffset? ReleasedAt,
    Guid AssignedByUserId,
    string? AssignedByUserName,
    bool IsActive);
