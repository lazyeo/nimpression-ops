using System.Text.Json.Serialization;

namespace Nimpression.Application.Features.Realtime.DTOs;

/// <summary>
/// 增量补齐变更条目 DTO。
/// 客户端断线重连后通过 HTTP 端点拉取遗漏的失效信号，用于本地缓存失效或状态刷新。
/// </summary>
public sealed record RealtimeChangeDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("entityId")] Guid EntityId,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt);
