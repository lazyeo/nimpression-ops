namespace Nimpression.Domain.Common;

/// <summary>
/// 领域事件标记接口。记录领域中已发生的事实。
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// 事件发生时间。
    /// </summary>
    DateTimeOffset OccurredAt { get; }
}
