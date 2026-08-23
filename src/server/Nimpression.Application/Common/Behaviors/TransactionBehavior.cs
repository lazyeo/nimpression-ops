using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Common.Behaviors;

/// <summary>
/// 给命令加事务边界。查询不进事务（无写操作，白白拿锁）。
///
/// 关键点：**失败的 <see cref="Result"/> 也要回滚**。若只在抛异常时回滚，
/// 一条"先写审计再发现无权限"的命令会把审计留下、业务撤销，
/// 造成审计与事实不一致。
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICommandMarker)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        TResponse response;
        try
        {
            response = await next(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (IsFailure(response))
        {
            await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return response;
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }

    private static bool IsFailure(TResponse response) => response switch
    {
        Result r => !r.IsSuccess,
        // Result<T> 是值类型泛型，无法用模式匹配统一处理，走接口约定读取。
        null => false,
        _ => response.GetType() is { IsGenericType: true } t
             && t.GetGenericTypeDefinition() == typeof(Result<>)
             && !(bool)t.GetProperty(nameof(Result.IsSuccess))!.GetValue(response)!,
    };
}
