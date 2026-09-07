using Nimpression.Domain.Common;

namespace Nimpression.Domain.Events;

/// <summary>
/// 任务已取消事件。
/// </summary>
public sealed record JobTaskCancelled(
    Guid JobTaskId,
    Guid? DriverId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
