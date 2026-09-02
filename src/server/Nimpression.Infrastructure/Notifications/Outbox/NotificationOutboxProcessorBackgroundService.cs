using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Infrastructure.Notifications.Outbox;

/// <summary>
/// 通知发件箱（Outbox）与邮件重试消费后台服务（F11.3）。
/// <para>
/// <b>核心设计：</b><br/>
/// 1. <b>进程重启可靠性（F11.3）</b>：业务数据提交后，即使进程中途被 kill，重启后后台服务仍会从数据库 OutboxMessages 与 EmailLogs 重新拉取并投递。<br/>
/// 2. <b>阶梯退避重试（F11.3）</b>：对失败邮件执行 1/5/25 分钟阶梯退避重试至多 3 次。<br/>
/// 3. <b>测试驱动友好</b>：对外暴露 <see cref="ProcessBatchAsync"/> 与 <see cref="ProcessMessageAsync"/>，消除集成测试中的定时器与轮询竞态。
/// </para>
/// </summary>
public sealed partial class NotificationOutboxProcessorBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationOutboxProcessorBackgroundService> logger,
    IOptions<NotificationOptions>? options = null) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = options?.Value.PollingInterval ?? TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedOutboxCount = 0;
            var retriedCount = 0;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var outboxService = scope.ServiceProvider.GetRequiredService<INotificationOutboxService>();

                processedOutboxCount = await outboxService.ProcessPendingOutboxMessagesAsync(stoppingToken);
                retriedCount = await outboxService.ProcessRetryQueueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogServiceLoopError(logger, ex);
            }

            if (processedOutboxCount == 0 && retriedCount == 0)
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
        }

        LogServiceStopped(logger);
    }

    /// <summary>
    /// 手动/测试驱动触发单批次 Outbox 消息处理。
    /// </summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<INotificationOutboxService>();
        return await outboxService.ProcessPendingOutboxMessagesAsync(cancellationToken);
    }

    /// <summary>
    /// 手动/测试驱动触发指定 ID 的单条 Outbox 消息处理。
    /// </summary>
    public async Task<bool> ProcessMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<INotificationOutboxService>();
        return await outboxService.ProcessOutboxMessageAsync(messageId, cancellationToken);
    }

    /// <summary>
    /// 手动/测试驱动触发单批次重试队列处理。
    /// </summary>
    public async Task<int> ProcessRetryBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<INotificationOutboxService>();
        return await outboxService.ProcessRetryQueueAsync(cancellationToken);
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "NotificationOutboxProcessorBackgroundService started.")]
    private static partial void LogServiceStarted(ILogger logger);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "NotificationOutboxProcessorBackgroundService stopped.")]
    private static partial void LogServiceStopped(ILogger logger);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Error,
        Message = "Unexpected error in NotificationOutboxProcessor background loop.")]
    private static partial void LogServiceLoopError(ILogger logger, Exception exception);
}
