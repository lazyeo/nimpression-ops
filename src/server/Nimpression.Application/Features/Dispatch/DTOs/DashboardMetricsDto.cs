namespace Nimpression.Application.Features.Dispatch.DTOs;

/// <summary>
/// 调度控制台聚合指标看板 DTO（精确对齐前端 DashboardMetricsDto 契约）。
/// </summary>
public sealed record DashboardMetricsDto(
    int ActiveDispatches,
    int OnlineDrivers,
    int PendingIncidents,
    int UnresolvedFines);
