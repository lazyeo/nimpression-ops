namespace Nimpression.Application.Features.Vehicles.DTOs;

/// <summary>
/// 车辆里程读数记录 DTO。
/// </summary>
public sealed record OdometerReadingDto(
    Guid Id,
    Guid VehicleId,
    Guid DriverId,
    string? DriverName,
    decimal ReadingKm,
    string? PhotoKey,
    DateTimeOffset RecordedAt,
    string Source);
