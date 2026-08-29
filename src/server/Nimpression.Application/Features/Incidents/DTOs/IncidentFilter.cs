using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Incidents.DTOs;

/// <summary>
/// 事故报告查询过滤参数（F9.4: 可按车辆/司机/时间范围/严重度查历史）。
/// </summary>
public sealed record IncidentFilter(
    Guid? DriverId = null,
    Guid? VehicleId = null,
    IncidentSeverity? Severity = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);
