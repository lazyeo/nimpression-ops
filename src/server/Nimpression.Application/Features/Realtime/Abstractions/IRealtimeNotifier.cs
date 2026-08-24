using Nimpression.Application.Features.Realtime.DTOs;

namespace Nimpression.Application.Features.Realtime.Abstractions;

/// <summary>
/// 实时推送通知契约。负责向 SignalR Hub 广播失效信号。
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>向指定分组推送失效信号</summary>
    Task PublishToGroupAsync(string groupName, RealtimeMessage message, CancellationToken cancellationToken = default);

    /// <summary>向指定用户推送失效信号</summary>
    Task PublishToUserAsync(Guid userId, RealtimeMessage message, CancellationToken cancellationToken = default);

    /// <summary>向指定司机推送失效信号</summary>
    Task PublishToDriverAsync(Guid driverId, RealtimeMessage message, CancellationToken cancellationToken = default);

    /// <summary>向指定角色全员推送失效信号</summary>
    Task PublishToRoleAsync(string role, RealtimeMessage message, CancellationToken cancellationToken = default);

    /// <summary>向所有已连接客户端广播失效信号</summary>
    Task PublishToAllAsync(RealtimeMessage message, CancellationToken cancellationToken = default);
}
