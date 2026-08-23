using System.Collections.Concurrent;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.Commands.ChangePassword;
using Nimpression.Application.Features.Identity.Commands.DeactivateUser;
using Nimpression.Application.Features.Identity.Commands.Login;
using Nimpression.Application.Features.Identity.Commands.Logout;
using Nimpression.Application.Features.Identity.Commands.RefreshToken;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Application.Features.Identity.Queries.GetUserById;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 身份认证与用户管理端点模块（F1 认证授权）。
/// 由 <see cref="EndpointModuleExtensions.MapEndpointModules"/> 自动发现并挂载。
/// </summary>
public sealed class IdentityEndpoints : IEndpointModule
{
    private const string RefreshTokenCookieName = "refreshToken";
    private const string CookiePath = "/api/auth";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var authGroup = routes.MapGroup("/api/auth")
            .WithTags("Auth");

        // F1.1 & N1.6: 登录（5次/分/IP限流，429 + Retry-After）
        authGroup.MapPost("/login", async (
            [FromBody] LoginRequest request,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var ip = GetClientIp(httpContext);
            if (!LoginRateLimiter.TryAcquire(ip, out var retryAfterSeconds))
            {
                httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return Results.Problem(
                    detail: "Too many login attempts. Please try again later.",
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "AUTH_RATE_LIMIT_EXCEEDED");
            }

            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var command = new LoginCommand(request.Email, request.Password, ip, userAgent);
            var result = await sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                return result.ToHttpResult();
            }

            SetRefreshTokenCookie(httpContext, result.Value.RawRefreshToken, result.Value.RefreshTokenExpiresAt);

            var response = new AuthSuccessResponse(
                result.Value.AccessToken,
                result.Value.ExpiresIn,
                result.Value.TokenType,
                result.Value.User);

            return Results.Ok(response);
        })
        .WithName("Login")
        .Produces<AuthSuccessResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // F1.2: 令牌刷新与轮转（旧令牌失效、重放攻击全撤销）
        authGroup.MapPost("/refresh", async (
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshRequest? bodyRequest,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var rawRefreshToken = !string.IsNullOrWhiteSpace(bodyRequest?.RefreshToken)
                ? bodyRequest.RefreshToken
                : httpContext.Request.Cookies[RefreshTokenCookieName]
                  ?? httpContext.Request.Headers["X-Refresh-Token"].FirstOrDefault();

            var ip = GetClientIp(httpContext);
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            var command = new RefreshTokenCommand(rawRefreshToken, ip, userAgent);
            var result = await sender.Send(command, cancellationToken);

            if (!result.IsSuccess)
            {
                return result.ToHttpResult();
            }

            SetRefreshTokenCookie(httpContext, result.Value.RawRefreshToken, result.Value.RefreshTokenExpiresAt);

            var response = new AuthSuccessResponse(
                result.Value.AccessToken,
                result.Value.ExpiresIn,
                result.Value.TokenType,
                result.Value.User);

            return Results.Ok(response);
        })
        .WithName("RefreshToken")
        .Produces<AuthSuccessResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        // F1.3: 登出（撤销当前 refresh token，清除 HttpOnly Cookie）
        authGroup.MapPost("/logout", async (
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RefreshRequest? bodyRequest,
            ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var rawRefreshToken = !string.IsNullOrWhiteSpace(bodyRequest?.RefreshToken)
                ? bodyRequest.RefreshToken
                : httpContext.Request.Cookies[RefreshTokenCookieName]
                  ?? httpContext.Request.Headers["X-Refresh-Token"].FirstOrDefault();

            var command = new LogoutCommand(rawRefreshToken);
            var result = await sender.Send(command, cancellationToken);

            ClearRefreshTokenCookie(httpContext);

            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .WithName("Logout")
        .Produces(StatusCodes.Status204NoContent);

        var usersGroup = routes.MapGroup("/api/users")
            .WithTags("Users");

        // N1.3: 用户资料查询（带越权防护：司机只能查自己，他人返回 403）
        usersGroup.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUserByIdQuery(id);
            var result = await sender.Send(query, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetUserById")
        .Produces<UserDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // F1.6: 修改密码（>=12位，越权防护）
        usersGroup.MapPost("/{id:guid}/change-password", async (
            Guid id,
            [FromBody] ChangePasswordRequestBody request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new ChangePasswordCommand(id, request.CurrentPassword, request.NewPassword);
            var result = await sender.Send(command, cancellationToken);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("ChangePassword")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // F1.5: 账号停用（仅管理员，停用后令牌 <=60s 失效）
        usersGroup.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new DeactivateUserCommand(id);
            var result = await sender.Send(command, cancellationToken);
            return result.ToHttpResult(StatusCodes.Status204NoContent);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DeactivateUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void SetRefreshTokenCookie(HttpContext context, string rawRefreshToken, DateTimeOffset expiresAt)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = CookiePath
        };

        context.Response.Cookies.Append(RefreshTokenCookieName, rawRefreshToken, cookieOptions);
    }

    private static void ClearRefreshTokenCookie(HttpContext context)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath
        };

        context.Response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
    }

    private static string GetClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded))
        {
            var ip = forwarded.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record ChangePasswordRequestBody(string CurrentPassword, string NewPassword);
public sealed record AuthSuccessResponse(
    string AccessToken,
    int ExpiresIn,
    string TokenType,
    AuthUserDto User);

/// <summary>
/// 登录端点固定窗口限流器（5次/分钟/IP）。
/// </summary>
public static class LoginRateLimiter
{
    private static readonly ConcurrentDictionary<string, List<DateTimeOffset>> RequestHistory = new();
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const int MaxRequestsPerWindow = 5;

    public static bool TryAcquire(string ip, out int retryAfterSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var list = RequestHistory.GetOrAdd(ip, _ => new List<DateTimeOffset>());

        lock (list)
        {
            list.RemoveAll(t => now - t > Window);
            if (list.Count >= MaxRequestsPerWindow)
            {
                var oldest = list[0];
                var waitTime = Window - (now - oldest);
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(waitTime.TotalSeconds));
                return false;
            }

            list.Add(now);
            retryAfterSeconds = 0;
            return true;
        }
    }

    public static void Reset()
    {
        RequestHistory.Clear();
    }
}
