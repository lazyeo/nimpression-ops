using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;

namespace Nimpression.Domain.Events;

/// <summary>
/// 事故已上报事件。
/// </summary>
public sealed record IncidentReported(
    Guid IncidentId,
    Guid DriverId,
    Guid VehicleId,
    IncidentSeverity Severity,
    DateTimeOffset OccurredAt) : IDomainEvent;
