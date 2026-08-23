using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using Nimpression.Application.Common.Abstractions;

namespace Nimpression.Application.Common.Behaviors;

/// <summary>
/// 结构化记录每条请求的名称、操作者与耗时。
/// 不记录请求体 —— 命令里含手机号、地址、密码等 PII（见 §4.2 数据分级），
/// 把它们写进日志等于绕过了字段级加密。
/// </summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUser currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var timestamp = Stopwatch.GetTimestamp();

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);

            // 显式 IsEnabled 短路：Stopwatch.GetElapsedTime 与 TotalMilliseconds
            // 在日志级别关闭时也会被求值（CA1873）。生成的委托内部虽有短路，
            // 但参数在调用点就已经算完了。
            if (logger.IsEnabled(LogLevel.Information))
            {
                var elapsedMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
                LogHandled(logger, requestName, currentUser.UserId, elapsedMs);
            }

            return response;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                var elapsedMs = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
                LogFailed(logger, ex, requestName, currentUser.UserId, elapsedMs);
            }

            throw;
        }
    }

    // 用 [LoggerMessage] 源生成器而非 logger.LogInformation(...)：
    // 后者每次调用都会装箱参数并格式化字符串，即使该级别已关闭（CA1848/CA1873）。
    // 生成的委托内建 IsEnabled 短路，热路径上零分配。
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Request {RequestName} handled for actor {ActorId} in {ElapsedMs}ms")]
    private static partial void LogHandled(ILogger logger, string requestName, Guid? actorId, double elapsedMs);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Request {RequestName} failed for actor {ActorId} after {ElapsedMs}ms")]
    private static partial void LogFailed(ILogger logger, Exception exception, string requestName, Guid? actorId, double elapsedMs);
}
