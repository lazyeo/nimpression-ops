using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Incidents.DTOs;

/// <summary>
/// 事故报告详情 DTO（包含照片预签名下载 URL）（F9.1 / F9.4）。
/// </summary>
public sealed record IncidentReportDetailDto(
    Guid Id,
    Guid DriverId,
    string DriverName,
    string EmployeeNo,
    Guid VehicleId,
    string VehicleRego,
    DateTimeOffset OccurredAt,
    string Location,
    IncidentSeverity Severity,
    string Description,
    string? ThirdPartyInfo,
    string Status,
    DateTimeOffset? InsurerNotifiedAt,
    IReadOnlyList<string> PhotoKeys,
    IReadOnlyList<string> PhotoUrls,
    bool NotifiedInsurer);
