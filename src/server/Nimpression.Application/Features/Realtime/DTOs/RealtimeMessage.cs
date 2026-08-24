using System.Text.Json.Serialization;

namespace Nimpression.Application.Features.Realtime.DTOs;

/// <summary>
/// 实时推送失效信号（Invalidation Signal）数据传输对象。
/// <para>
/// <b>核心设计约束：推送只作「失效信号」，不作数据通道。</b><br/>
/// Hub 推送的消息<b>只携带“什么变了”，绝不携带业务数据本身</b>。<br/>
/// 客户端收到失效信号后，走权威的 HTTP REST API 重新拉取最新数据。
/// </para>
/// <para>
/// <b>架构设计理由：</b><br/>
/// 1. 推送通道不可信也不可靠——消息可能丢失、乱序、重放、被篡改。<br/>
///    若业务数据只从推送来，通道一出问题业务数据就损坏或产生状态漂移。<br/>
/// 2. 只推失效信号时，篡改推送内容不影响业务正确性——<br/>
///    最坏结果是客户端多拉一次或少拉一次，而少拉由重连后的增量补齐兜住。<br/>
/// 3. 这直接消解了原项目“WebSocket 崩了业务就乱”的系统性根因。
/// </para>
/// </summary>
/// <param name="Kind">变更事件类型标识（如 "task.assigned", "task.completed"）</param>
/// <param name="EntityId">发生变更的聚合根或实体全局唯一标识</param>
/// <param name="OccurredAt">领域事件产生的 UTC 时间戳</param>
public sealed record RealtimeMessage(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("entityId")] Guid EntityId,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt);
