using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Common.Abstractions;

namespace Nimpression.Infrastructure.Persistence;

/// <summary>
/// 事务边界实现。管理 EF Core 事务与变更持久化。
/// 支持嵌套范围复用，避免 Npgsql 嵌套事务报错。
/// </summary>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    private int _transactionDepth;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction == null)
        {
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        _transactionDepth++;
        return new TransactionScope(this);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (_transactionDepth <= 1)
            {
                await dbContext.Database.CommitTransactionAsync(cancellationToken);
            }
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction != null)
        {
            await dbContext.Database.RollbackTransactionAsync(cancellationToken);
        }
    }

    private sealed class TransactionScope(UnitOfWork uow) : IAsyncDisposable
    {
        private bool _disposed;

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                uow._transactionDepth--;
                if (uow._transactionDepth <= 0 && uow.dbContext.Database.CurrentTransaction != null)
                {
                    await uow.dbContext.Database.CurrentTransaction.DisposeAsync();
                }
            }
        }
    }
}
