using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Vehicles.DTOs;

/// <summary>
/// 车辆列表投影摘要 DTO。避免 N+1 实体加载，满足 N3.6 投影要求。
/// </summary>
public sealed record VehicleSummaryDto(
    Guid Id,
    string Rego,
    string Make,
    string Model,
    int Year,
    decimal OdometerKm,
    decimal ServiceIntervalKm,
    decimal LastServiceOdometerKm,
    decimal DistanceSinceLastServiceKm,
    bool IsServiceDue,
    DateOnly? WofExpiry,
    DateOnly? CofExpiry,
    DateOnly? InsuranceExpiry,
    VehicleStatus Status,
    Guid? CurrentDriverId,
    string? CurrentDriverName);
