using System.Reflection;

namespace Nimpression.Application.Common.Results;

/// <summary>
/// 管道行为需要在不知道 <c>TResponse</c> 具体类型的情况下构造失败结果。
/// 这里把反射集中到一处并缓存，避免每个 Behavior 各写一份。
/// </summary>
public static class ResultFactory
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo> FailureMethods = new();

    /// <summary>把 <paramref name="error"/> 包装成 <typeparamref name="TResponse"/> 形态的失败结果。</summary>
    public static TResponse FailureOf<TResponse>(Error error)
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var method = FailureMethods.GetOrAdd(
                responseType,
                static t => t.GetMethod(nameof(Result<object>.Failure), BindingFlags.Public | BindingFlags.Static)!);

            return (TResponse)method.Invoke(null, [error])!;
        }

        throw new InvalidOperationException(
            $"管道行为只能作用于返回 Result 或 Result<T> 的请求，但 {responseType.Name} 不是。" +
            "把该请求的返回类型改成 Result 形态，否则失败无法在不抛异常的前提下传播。");
    }
}
