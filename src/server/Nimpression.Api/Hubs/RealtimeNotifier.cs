using Microsoft.AspNetCore.SignalR;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Application.Features.Realtime.DTOs;

namespace Nimpression.Api.Hubs;

/// <summary>
/// 基于 ASP.NET Core SignalR <see cref="IHubContext{THub, T}"/> 的实时失效信号广播实现。
/// </summary>
public sealed class RealtimeNotifier(IHubContext<RealtimeHub, IRealtimeClient> hubContext) : IRealtimeNotifier
{
    public async Task PublishToGroupAsync(string groupName, RealtimeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentNullException.ThrowIfNull(message);

        await hubContext.Clients.Group(groupName).ReceiveInvalidation(message);
    }

    public async Task PublishToUserAsync(Guid userId, RealtimeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var groupName = RealtimeGroupNames.User(userId);
        await hubContext.Clients.Group(groupName).ReceiveInvalidation(message);
    }

    public async Task PublishToDriverAsync(Guid driverId, RealtimeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var groupName = RealtimeGroupNames.Driver(driverId);
        await hubContext.Clients.Group(groupName).ReceiveInvalidation(message);
    }

    public async Task PublishToRoleAsync(string role, RealtimeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(message);

        var groupName = RealtimeGroupNames.Role(role);
        await hubContext.Clients.Group(groupName).ReceiveInvalidation(message);
    }

    public async Task PublishToAllAsync(RealtimeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await hubContext.Clients.All.ReceiveInvalidation(message);
    }
}
