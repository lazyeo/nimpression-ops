using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Incidents.DTOs;

/// <summary>
/// 事故报告列表项 DTO（F9.1 / F9.4）。
/// </summary>
public sealed record IncidentReportDto(
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
    bool NotifiedInsurer);
