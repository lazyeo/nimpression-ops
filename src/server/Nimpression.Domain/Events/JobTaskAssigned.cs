using Nimpression.Domain.Common;

namespace Nimpression.Domain.Events;

/// <summary>
/// 任务已指派事件。
/// </summary>
public sealed record JobTaskAssigned(
    Guid JobTaskId,
    Guid DriverId,
    Guid VehicleId,
    DateTimeOffset OccurredAt) : IDomainEvent;
