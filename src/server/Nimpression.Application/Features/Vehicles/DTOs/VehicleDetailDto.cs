using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Vehicles.DTOs;

/// <summary>
/// 车辆详情 DTO。包含完整车辆属性、当前分派与最近里程信息。
/// </summary>
public sealed record VehicleDetailDto(
    Guid Id,
    string Rego,
    string Make,
    string Model,
    int Year,
    string VinEnc,
    decimal OdometerKm,
    decimal ServiceIntervalKm,
    decimal LastServiceOdometerKm,
    decimal DistanceSinceLastServiceKm,
    bool IsServiceDue,
    DateOnly? WofExpiry,
    DateOnly? CofExpiry,
    DateOnly? InsuranceExpiry,
    VehicleStatus Status,
    VehicleAssignmentDto? ActiveAssignment,
    OdometerReadingDto? LatestOdometerReading);
