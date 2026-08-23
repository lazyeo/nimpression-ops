namespace Nimpression.Domain.Common;

/// <summary>
/// 领域实体基类。
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        Id = id;
    }
}
