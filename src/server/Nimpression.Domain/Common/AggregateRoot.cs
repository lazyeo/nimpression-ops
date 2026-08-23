namespace Nimpression.Domain.Common;

/// <summary>
/// 聚合根基类，维护领域事件集合。
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// 当前聚合根收集的领域事件（只读）。
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id) : base(id)
    {
    }

    /// <summary>
    /// 添加领域事件。
    /// </summary>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// 清空领域事件（通常由持久化层在发布事件后调用）。
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
