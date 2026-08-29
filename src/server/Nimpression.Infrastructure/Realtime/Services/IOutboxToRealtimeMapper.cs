using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Entities.Standalone;

namespace Nimpression.Infrastructure.Realtime.Services;

/// <summary>
/// 映射结果，包含纯失效信号、目标推送分组列表以及关联的司机 ID（若有）。
/// </summary>
public sealed record OutboxRealtimeMapping(
    RealtimeMessage Message,
    IReadOnlyList<string> TargetGroups,
    Guid? TargetDriverId);

/// <summary>
/// 将持久化的 Outbox 领域事件记录解析并映射为无业务数据的纯失效信号与目标推送分组。
/// </summary>
public interface IOutboxToRealtimeMapper
{
    /// <summary>
    /// 将 OutboxMessage 映射为实时失效信号及目标投递分组。
    /// </summary>
    OutboxRealtimeMapping Map(OutboxMessage outboxMessage);

    /// <summary>
    /// 将指定类型名、JSON Payload 及产生时间映射为实时失效信号及目标投递分组。
    /// </summary>
    OutboxRealtimeMapping Map(string eventType, string payloadJson, DateTimeOffset occurredAt);
}
