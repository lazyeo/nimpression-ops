using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Nimpression.Infrastructure.Realtime.Security;

/// <summary>
/// 为 SignalR 实时 Hub 连接配置 JWT 访问令牌提取。
/// 由于浏览器 WebSocket 握手无法添加自定义 HTTP Header，SignalR 客户端在握手请求中使用 Query String 传递 ?access_token=...。
/// 此设置截获 /hubs 路径的请求并从 Query 提取 JWT 赋给 context.Token。
/// </summary>
public sealed class RealtimeJwtBearerOptionsSetup : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        var existingOnMessageReceived = options.Events?.OnMessageReceived;

        options.Events ??= new JwtBearerEvents();
        options.Events.OnMessageReceived = async context =>
        {
            if (existingOnMessageReceived is not null)
            {
                await existingOnMessageReceived(context);
            }

            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            // 仅对 SignalR Hub 路由提取 query token，防止普通 API 端点误用 query 传鉴权令牌
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
        };
    }
}
