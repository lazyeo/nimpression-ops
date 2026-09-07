using Nimpression.Domain.Common;

namespace Nimpression.Domain.Events;

/// <summary>
/// 任务已开始执行事件。
/// </summary>
public sealed record JobTaskStarted(
    Guid JobTaskId,
    Guid DriverId,
    DateTimeOffset OccurredAt) : IDomainEvent;
