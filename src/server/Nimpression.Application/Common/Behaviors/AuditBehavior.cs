using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Common.Behaviors;

/// <summary>
/// 为实现 <see cref="IAuditableCommand"/> 的命令写审计（N1.1）。
///
/// 只在**成功**后写：失败的命令没有改变任何事实，为它留一条审计
/// 会让"谁改了什么"的查询混入大量未发生的变更。
/// 越权尝试的记录属于安全日志，走另一条路（N1.3），不占审计表。
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse>(IAuditSink auditSink)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);

        if (request is not IAuditableCommand auditable || IsFailure(response))
        {
            return response;
        }

        await auditSink.RecordAsync(
            auditable.AuditEntityType,
            auditable.AuditEntityId,
            auditable.AuditAction,
            beforeJson: null,
            afterJson: null,
            cancellationToken).ConfigureAwait(false);

        return response;
    }

    private static bool IsFailure(TResponse response) => response switch
    {
        Result r => !r.IsSuccess,
        null => false,
        _ => response.GetType() is { IsGenericType: true } t
             && t.GetGenericTypeDefinition() == typeof(Result<>)
             && !(bool)t.GetProperty(nameof(Result.IsSuccess))!.GetValue(response)!,
    };
}
