using Nimpression.Application.Common.Results;

namespace Nimpression.Api.Common;

/// <summary>
/// 把应用层的 <see cref="Result"/> 翻译成 HTTP 响应。
/// 集中在一处，保证所有端点对同一种失败给出同一个状态码 ——
/// 否则"越权返回 403 还是 404"这类决定会散落在几十个端点里各写各的。
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, int successStatusCode = StatusCodes.Status204NoContent)
        => result.IsSuccess
            ? Results.StatusCode(successStatusCode)
            : Problem(result.Error!);

    public static IResult ToHttpResult<TValue>(this Result<TValue> result, int successStatusCode = StatusCodes.Status200OK)
        => result.IsSuccess
            ? successStatusCode == StatusCodes.Status201Created
                ? Results.Created((string?)null, result.Value)
                : Results.Ok(result.Value)
            : Problem(result.Error!);

    /// <summary>按 RFC 9457 返回 application/problem+json（N1.4）。</summary>
    private static IResult Problem(Error error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            // 存在但无权访问返回 403 而非 404：见 Error.cs 的说明
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.UnprocessableEntity => StatusCodes.Status422UnprocessableEntity,
            ErrorKind.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError,
        };

        return error.Details is { Count: > 0 }
            ? Results.ValidationProblem(
                errors: error.Details.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
                detail: error.Message,
                statusCode: status,
                title: error.Code)
            : Results.Problem(detail: error.Message, statusCode: status, title: error.Code);
    }
}
