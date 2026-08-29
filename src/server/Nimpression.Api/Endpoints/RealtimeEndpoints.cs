using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Application.Features.Realtime.Queries.GetRecentChanges;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 实时通信增量补齐端点模块（F12.3）。
/// 允许客户端断线重连后拉取指定时间点之后遗漏的失效信号。
/// 由程序集扫描自动发现并挂载。
/// </summary>
public sealed class RealtimeEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/realtime")
            .WithTags("Realtime")
            .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser);

        group.MapGet("/changes", async (
            [FromQuery] DateTimeOffset since,
            [FromQuery] int? limit,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetRecentChangesQuery(since, limit ?? 100), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithName("GetRecentChanges")
        .Produces<List<RealtimeChangeDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
