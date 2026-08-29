using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Fines.DTOs;

/// <summary>
/// 交通罚单列表项 DTO（F8.1 / F8.2）。
/// </summary>
public sealed record FineDto(
    Guid Id,
    Guid DriverId,
    string DriverName,
    string EmployeeNo,
    Guid VehicleId,
    string VehicleRego,
    DateOnly IssuedOn,
    string Authority,
    string Reference,
    decimal Amount,
    string Currency,
    string Reason,
    FineStatus Status,
    string? TicketPhotoKey,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);
