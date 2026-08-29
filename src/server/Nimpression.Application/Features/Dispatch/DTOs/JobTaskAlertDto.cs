using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Dispatch.DTOs;

/// <summary>
/// 未确认派发任务预警 DTO（F5.5）。
/// </summary>
public sealed record JobTaskAlertDto(
    Guid TaskId,
    string Ref,
    string Title,
    Guid DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehicleRego,
    Guid AreaId,
    string AreaName,
    DateTimeOffset ScheduledFor,
    int MinutesUnacknowledged);
