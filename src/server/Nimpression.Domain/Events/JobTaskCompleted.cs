using Nimpression.Domain.Common;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Events;

/// <summary>
/// 任务已完工事件。
/// </summary>
public sealed record JobTaskCompleted(
    Guid JobTaskId,
    Guid DriverId,
    Kilometres? DistanceKm,
    DateTimeOffset OccurredAt) : IDomainEvent;
