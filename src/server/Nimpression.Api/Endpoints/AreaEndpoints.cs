using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Areas.Commands.AssignDriverToArea;
using Nimpression.Application.Features.Areas.Commands.CreateArea;
using Nimpression.Application.Features.Areas.Commands.DeleteArea;
using Nimpression.Application.Features.Areas.Commands.EndAreaAssignment;
using Nimpression.Application.Features.Areas.Commands.UpdateArea;
using Nimpression.Application.Features.Areas.DTOs;
using Nimpression.Application.Features.Areas.Queries.GetAreaAssignments;
using Nimpression.Application.Features.Areas.Queries.GetAreaById;
using Nimpression.Application.Features.Areas.Queries.GetAreasList;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 运营区域与区域分配 Minimal API 端点模块（F4.1–F4.3）。
/// 由 IEndpointModule 自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class AreaEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/areas")
            .WithTags("Areas");

        // F4.1: 区域列表查询
        group.MapGet("/", async (
            [FromQuery] string? searchTerm,
            [FromQuery] bool? isActive,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new AreaFilter(searchTerm, isActive, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(new GetAreasListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetAreasList")
        .WithSummary("获取运营区域列表");

        // F4.1: 按 ID 获取区域详情
        group.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAreaByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetAreaById")
        .WithSummary("按 ID 获取运营区域详情");

        // F4.1: 创建区域
        group.MapPost("/", async (
            [FromBody] CreateAreaRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateAreaCommand(
                request.Name,
                request.Code,
                request.Description,
                request.GeoJson,
                request.IsActive ?? true);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("CreateArea")
        .WithSummary("创建运营区域（区域代码全局唯一，冲突返回 409）");

        // F4.1: 修改区域
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAreaRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new UpdateAreaCommand(
                id,
                request.Name,
                request.Code,
                request.Description,
                request.GeoJson,
                request.IsActive ?? true);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("UpdateArea")
        .WithSummary("修改运营区域信息");

        // F4.1: 删除区域（有生效中分配返回 409）
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new DeleteAreaCommand(id);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("DeleteArea")
        .WithSummary("删除运营区域（存在生效中司机分配时拒绝并返回 409）");

        // F4.2: 为司机分配区域
        group.MapPost("/{id:guid}/assignments", async (
            Guid id,
            [FromBody] AssignDriverToAreaRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new AssignDriverToAreaCommand(
                request.DriverId,
                id,
                request.EffectiveFrom,
                request.EffectiveTo);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("AssignDriverToArea")
        .WithSummary("为司机分配区域（同一司机同一区域生效期不可重叠，重叠返回 422 并指出冲突区间）");

        // F4.2: 结束司机区域分配
        group.MapPost("/assignments/{assignmentId:guid}/end", async (
            Guid assignmentId,
            [FromBody] EndAreaAssignmentRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new EndAreaAssignmentCommand(assignmentId, request.EffectiveTo);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("EndAreaAssignment")
        .WithSummary("设置司机区域分配的结束日期");

        // F4.2: 获取区域的司机分配列表
        group.MapGet("/{id:guid}/assignments", async (
            Guid id,
            [FromQuery] Guid? driverId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAreaAssignmentsQuery(id, driverId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetAreaAssignments")
        .WithSummary("获取指定区域的司机分配列表");

        // F4.2: 查询分配列表（支持按司机筛选）
        group.MapGet("/assignments", async (
            [FromQuery] Guid? areaId,
            [FromQuery] Guid? driverId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAreaAssignmentsQuery(areaId, driverId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetAllAreaAssignments")
        .WithSummary("查询所有区域分配列表（可按区域或司机过滤）");
    }
}

public sealed record CreateAreaRequest(
    string Name,
    string Code,
    string? Description = null,
    string? GeoJson = null,
    bool? IsActive = true);

public sealed record UpdateAreaRequest(
    string Name,
    string Code,
    string? Description = null,
    string? GeoJson = null,
    bool? IsActive = true);

public sealed record AssignDriverToAreaRequest(
    Guid DriverId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo = null);

public sealed record EndAreaAssignmentRequest(DateOnly EffectiveTo);
