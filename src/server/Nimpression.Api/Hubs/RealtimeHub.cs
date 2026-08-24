using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Hubs;

/// <summary>
/// 实时通信 SignalR Hub。
/// <para>
/// <b>核心设计与安全约束：</b><br/>
/// 1. <b>F12.1 鉴权连接</b>：挂载 <see cref="AuthorizeAttribute"/>，要求连接握手必须携带有效 JWT；无效/过期 token 握手阶段直接拒绝（401），绝非连上后再踢。<br/>
/// 2. <b>F12.2 分组隔离</b>：连接建立时，自动将客户端加入广播组（all）、角色组（role:Admin / role:Dispatcher / role:Driver）以及司机私有组（driver:{driverId}）。<br/>
///    司机 A 绝无法收到发往司机 B 的专属消息。<br/>
/// 3. <b>推送只作「失效信号」</b>：Hub 推送的消息只携带 { kind, entityId, occurredAt }，绝不携带业务数据本身。
/// </para>
/// </summary>
[Authorize(AuthorizationPolicies.AuthenticatedUser)]
public sealed partial class RealtimeHub(
    IDriverRepository driverRepository,
    ILogger<RealtimeHub> logger) : Hub<IRealtimeClient>
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var connectionId = Context.ConnectionId;

        var userIdStr = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value;

        var roleStr = user?.FindFirst(ClaimTypes.Role)?.Value
            ?? user?.FindFirst("role")?.Value;

        LogClientConnected(logger, connectionId, userIdStr, roleStr);

        // 1. 加入全员广播组
        await Groups.AddToGroupAsync(connectionId, RealtimeGroupNames.All);

        // 2. 加入角色组
        if (!string.IsNullOrWhiteSpace(roleStr))
        {
            await Groups.AddToGroupAsync(connectionId, RealtimeGroupNames.Role(roleStr));
        }

        // 3. 加入用户私有组与司机专属组
        if (Guid.TryParse(userIdStr, out var userId))
        {
            await Groups.AddToGroupAsync(connectionId, RealtimeGroupNames.User(userId));

            if (string.Equals(roleStr, UserRole.Driver.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var driver = await driverRepository.GetByUserIdAsync(userId);
                if (driver is not null)
                {
                    await Groups.AddToGroupAsync(connectionId, RealtimeGroupNames.Driver(driver.Id));
                    LogDriverGroupJoined(logger, connectionId, driver.Id);
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        LogClientDisconnected(logger, connectionId, userIdStr, exception);

        await base.OnDisconnectedAsync(exception);
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "RealtimeHub client connected: ConnectionId={ConnectionId}, UserId={UserId}, Role={Role}")]
    private static partial void LogClientConnected(ILogger logger, string connectionId, string? userId, string? role);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "RealtimeHub driver joined private group: ConnectionId={ConnectionId}, DriverId={DriverId}")]
    private static partial void LogDriverGroupJoined(ILogger logger, string connectionId, Guid driverId);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "RealtimeHub client disconnected: ConnectionId={ConnectionId}, UserId={UserId}")]
    private static partial void LogClientDisconnected(ILogger logger, string connectionId, string? userId, Exception? exception);
}
