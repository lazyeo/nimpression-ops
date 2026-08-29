using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Dispatch.DTOs;

/// <summary>
/// 派发任务完整详情 DTO。
/// </summary>
public sealed record JobTaskDetailDto(
    Guid Id,
    string Ref,
    string Title,
    string? Description,
    Guid AreaId,
    string AreaName,
    string AreaCode,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehicleRego,
    DateTimeOffset ScheduledFor,
    TaskPriority Priority,
    JobTaskStatus Status,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    Guid CreatedByUserId,
    string? CreatedByUserName,
    decimal? PlannedDistanceKm,
    decimal? ActualDistanceKm,
    decimal? StartOdometerKm,
    decimal? EndOdometerKm,
    decimal? EffectiveDistanceKm);
