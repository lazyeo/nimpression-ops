using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Domain.Enums;

namespace Nimpression.Infrastructure.Security;

/// <summary>
/// 基于 ASP.NET Core HttpContext 的当前用户上下文实现。
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var user = HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value
                          ?? user.FindFirst("uid")?.Value;

            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var user = HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value
                            ?? user.FindFirst("role")?.Value;

            if (string.IsNullOrWhiteSpace(roleClaim))
            {
                return null;
            }

            if (Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role))
            {
                return role;
            }

            if (int.TryParse(roleClaim, out var roleInt) && Enum.IsDefined(typeof(UserRole), roleInt))
            {
                return (UserRole)roleInt;
            }

            return null;
        }
    }

    public string? IpAddress
    {
        get
        {
            var context = HttpContext;
            if (context is null)
            {
                return null;
            }

            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) &&
                !string.IsNullOrWhiteSpace(forwarded))
            {
                var ip = forwarded.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    return ip;
                }
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent
    {
        get
        {
            var agent = HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(agent) ? null : agent;
        }
    }

    public bool IsAuthenticated => HttpContext?.User.Identity?.IsAuthenticated == true && UserId.HasValue;
}
