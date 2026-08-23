using Nimpression.Domain.Common;

namespace Nimpression.Domain.Events;

/// <summary>
/// 司机确认接受任务事件。
/// </summary>
public sealed record JobTaskAcknowledged(
    Guid JobTaskId,
    Guid DriverId,
    DateTimeOffset OccurredAt) : IDomainEvent;
