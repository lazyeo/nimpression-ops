using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Dispatch.DTOs;

/// <summary>
/// 派发任务概要 DTO。
/// </summary>
public sealed record JobTaskSummaryDto(
    Guid Id,
    string Ref,
    string Title,
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
    decimal? PlannedDistanceKm,
    decimal? EffectiveDistanceKm);
