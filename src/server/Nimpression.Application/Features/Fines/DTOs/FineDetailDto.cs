using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Fines.DTOs;

/// <summary>
/// 交通罚单详情 DTO（包含审核人与短时效预签名 URL）（F8.1 / F8.4）。
/// </summary>
public sealed record FineDetailDto(
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
    string? TicketPhotoUrl,
    Guid? ReviewedByUserId,
    string? ReviewerName,
    DateTimeOffset? ReviewedAt,
    string? ReviewNote);
