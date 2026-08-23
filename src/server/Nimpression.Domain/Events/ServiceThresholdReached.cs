using Nimpression.Domain.Common;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Events;

/// <summary>
/// 车辆里程达到保养阈值事件。
/// </summary>
public sealed record ServiceThresholdReached(
    Guid VehicleId,
    int ServiceCycleNo,
    Kilometres CurrentOdometerKm,
    Kilometres ThresholdKm,
    DateTimeOffset OccurredAt) : IDomainEvent;
