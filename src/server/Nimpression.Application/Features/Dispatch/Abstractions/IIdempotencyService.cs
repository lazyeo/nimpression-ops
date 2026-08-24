using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Dispatch.Abstractions;

/// <summary>
/// 离线重放幂等服务抽象（F5.4）。
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// 以幂等方式执行无返回值的业务操作。
    /// </summary>
    Task<Result> ExecuteAsync(
        string key,
        object requestPayload,
        Func<Task<Result>> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 以幂等方式执行带返回值的业务操作。
    /// </summary>
    Task<Result<TResponse>> ExecuteAsync<TResponse>(
        string key,
        object requestPayload,
        Func<Task<Result<TResponse>>> action,
        CancellationToken cancellationToken = default);
}
