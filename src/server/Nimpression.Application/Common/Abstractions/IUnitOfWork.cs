namespace Nimpression.Application.Common.Abstractions;

/// <summary>
/// 事务边界。<see cref="Behaviors.TransactionBehavior{TRequest,TResponse}"/> 用它把
/// 一条命令的全部写操作（含领域事件落 Outbox）包进同一个事务。
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
