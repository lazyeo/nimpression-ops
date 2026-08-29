using Nimpression.Application.Features.Realtime.DTOs;

namespace Nimpression.Api.Hubs;

/// <summary>
/// 强类型 SignalR 客户端回调契约。
/// 客户端接收到失效信号后，根据 Kind 和 EntityId 自行发起 HTTP 请求刷新本地数据。
/// </summary>
public interface IRealtimeClient
{
    /// <summary>
    /// 接收纯失效信号。
    /// 绝不包含具体实体业务数据，仅包含 { kind, entityId, occurredAt }。
    /// </summary>
    Task ReceiveInvalidation(RealtimeMessage message);
}
