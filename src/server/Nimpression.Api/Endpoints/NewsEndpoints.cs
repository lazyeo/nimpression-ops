using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.News.Commands.CreateNewsPost;
using Nimpression.Application.Features.News.Commands.MarkNewsAsRead;
using Nimpression.Application.Features.News.DTOs;
using Nimpression.Application.Features.News.Queries.GetNewsById;
using Nimpression.Application.Features.News.Queries.GetNewsList;
using Nimpression.Application.Features.News.Queries.GetNewsReadStats;
using Nimpression.Application.Features.News.Queries.GetNewsUnreadUsers;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 新闻公告 Minimal API 端点模块（F10.1–F10.3）。
/// 由 IEndpointModule 自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class NewsEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/news")
            .WithTags("News");

        // F10.1: 发布新闻公告（仅 Admin 可发布，返回 201 Created）
        group.MapPost("/", async (
            [FromBody] CreateNewsPostRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateNewsPostCommand(
                request.Title,
                request.BodyEn,
                request.BodyZh,
                request.Audience,
                request.Pinned ?? false);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("CreateNewsPost")
        .WithSummary("发布新闻公告（双语正文必填，缺一 422）");

        // F10.1: 查询新闻公告列表（置顶优先，同置顶按发布时间倒序）
        group.MapGet("/", async (
            [FromQuery] NewsAudience? audience,
            [FromQuery] bool? isPinned,
            [FromQuery] bool? isActive,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new NewsListFilter(
                audience,
                isPinned,
                isActive ?? true,
                page ?? 1,
                pageSize ?? 20);

            var result = await sender.Send(new GetNewsListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetNewsList")
        .WithSummary("获取新闻公告列表（已认证用户）");

        // F10.1: 按 ID 获取新闻详情
        group.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNewsByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetNewsById")
        .WithSummary("获取指定新闻公告详情");

        // F10.2: 标记已读回执（幂等处理）
        group.MapPost("/{id:guid}/read", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new MarkNewsAsReadCommand(id);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("MarkNewsAsRead")
        .WithSummary("记录新闻已读回执（同一人重复打开幂等返回）");

        // F10.2: 管理端查询已读统计（如 7/10 及已读率）
        group.MapGet("/{id:guid}/stats", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNewsReadStatsQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetNewsReadStats")
        .WithSummary("管理端查询新闻公告已读统计（分母排除停用账号）");

        // F10.2: 管理端查询未读人员名单
        group.MapGet("/{id:guid}/unread", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetNewsUnreadUsersQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetNewsUnreadUsers")
        .WithSummary("管理端查询新闻公告未读名单（排除停用账号）");
    }
}

public sealed record CreateNewsPostRequest(
    string Title,
    string BodyEn,
    string BodyZh,
    NewsAudience Audience,
    bool? Pinned = false);
