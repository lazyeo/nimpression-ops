using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Realtime.Services;

namespace Nimpression.Infrastructure.Realtime.BackgroundServices;

/// <summary>
/// Outbox 发件箱消息消费后台服务。
/// <para>
/// <b>核心设计：</b><br/>
/// 1. <b>至少一次投递（At-least-once）</b>：从 OutboxMessages 抓取未处理消息推送到 SignalR Hub，推送失败时记录尝试与异常并重试，绝不静默丢失。<br/>
/// 2. <b>天然幂等（Idempotent）</b>：由于推送的内容为纯失效信号，客户端收到后仅触发本地拉取，重复推送不会产生任何业务副作用。<br/>
/// 3. <b>状态追踪</b>：投递成功后更新 <see cref="OutboxMessage.ProcessedAt"/>，进程重启后绝不重复投递已处理记录。
/// </para>
/// </summary>
public sealed partial class OutboxProcessorBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessorBackgroundService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(200);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedCount = 0;
            try
            {
                processedCount = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWorkerLoopError(logger, ex);
            }

            if (processedCount == 0)
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
        }

        LogWorkerStopped(logger);
    }

    /// <summary>
    /// 处理单批次 Outbox 消息。供后台循环及集成测试直接调用以做确定性验证。
    /// </summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mapper = scope.ServiceProvider.GetRequiredService<IOutboxToRealtimeMapper>();
        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();
        var dateTimeProvider = scope.ServiceProvider.GetService<IDateTimeProvider>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        var now = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;

        foreach (var message in messages)
        {
            try
            {
                var mapping = mapper.Map(message);

                foreach (var group in mapping.TargetGroups)
                {
                    await realtimeNotifier.PublishToGroupAsync(group, mapping.Message, cancellationToken);
                }

                message.MarkProcessed(now);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogDeliveryFailed(logger, ex, message.Id, message.Type, message.Attempts + 1);
                message.RecordAttempt(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "OutboxProcessorBackgroundService started.")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "OutboxProcessorBackgroundService stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Unexpected error in OutboxProcessor background worker loop.")]
    private static partial void LogWorkerLoopError(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Error,
        Message = "Failed to deliver OutboxMessage {MessageId} ({Type}) on attempt {Attempt}")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, Guid messageId, string type, int attempt);
}
