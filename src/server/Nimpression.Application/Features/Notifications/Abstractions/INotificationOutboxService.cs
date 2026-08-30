using Nimpression.Domain.Entities.Standalone;

namespace Nimpression.Application.Features.Notifications.Abstractions;

/// <summary>
/// 通知发件箱（Outbox）消费与重试服务接口（F11.3, F11.4）。
/// 统一处理领域事件落库后的邮件投递、幂等去重以及失败退避重试。
/// </summary>
public interface INotificationOutboxService
{
    /// <summary>
    /// 处理单批次待发送的 Outbox 消息并触发对应邮件投递。
    /// </summary>
    Task<int> ProcessPendingOutboxMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理单条指定 ID 的 Outbox 消息（供测试驱动与确定性断言）。
    /// </summary>
    Task<bool> ProcessOutboxMessageAsync(Guid outboxMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行失败邮件日志的退避重试（按 1/5/25 分钟退避，最多 3 次）。
    /// </summary>
    Task<int> ProcessRetryQueueAsync(CancellationToken cancellationToken = default);
}
