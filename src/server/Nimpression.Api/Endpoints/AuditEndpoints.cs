using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Application.Features.Identity.Queries.ExportAuditLogs;
using Nimpression.Application.Features.Identity.Queries.GetAuditLogs;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 审计日志查询与导出端点模块（N1.2 审计可查）。
/// 由 <see cref="EndpointModuleExtensions.MapEndpointModules"/> 自动发现并挂载。
/// </summary>
public sealed class AuditEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var auditGroup = routes.MapGroup("/api/audit-logs")
            .WithTags("Auditing")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        // N1.2: 审计日志多维检索与分页
        auditGroup.MapGet("/", async (
            [FromQuery] Guid? actorUserId,
            [FromQuery] string? entityType,
            [FromQuery] string? entityId,
            [FromQuery] string? action,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetAuditLogsQuery(
                actorUserId,
                entityType,
                entityId,
                action,
                from,
                to,
                page.HasValue && page.Value > 0 ? page.Value : 1,
                pageSize.HasValue && pageSize.Value > 0 ? pageSize.Value : 20);

            var result = await sender.Send(query, cancellationToken);
            return result.ToHttpResult();
        })
        .WithName("GetAuditLogs")
        .Produces<PagedResult<AuditEventDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // N1.2: 审计日志导出 CSV
        auditGroup.MapGet("/export", async (
            [FromQuery] Guid? actorUserId,
            [FromQuery] string? entityType,
            [FromQuery] string? entityId,
            [FromQuery] string? action,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new ExportAuditLogsQuery(
                actorUserId,
                entityType,
                entityId,
                action,
                from,
                to);

            var result = await sender.Send(query, cancellationToken);
            if (!result.IsSuccess)
            {
                return result.ToHttpResult();
            }

            return Results.File(
                result.Value.Bytes,
                contentType: result.Value.ContentType,
                fileDownloadName: result.Value.FileName);
        })
        .WithName("ExportAuditLogs")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv")
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
