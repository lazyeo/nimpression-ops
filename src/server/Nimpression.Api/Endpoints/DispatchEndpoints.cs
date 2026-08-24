using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.Commands.AcknowledgeJobTask;
using Nimpression.Application.Features.Dispatch.Commands.AssignJobTask;
using Nimpression.Application.Features.Dispatch.Commands.CancelJobTask;
using Nimpression.Application.Features.Dispatch.Commands.CompleteJobTask;
using Nimpression.Application.Features.Dispatch.Commands.CreateJobTask;
using Nimpression.Application.Features.Dispatch.Commands.StartJobTask;
using Nimpression.Application.Features.Dispatch.DTOs;
using Nimpression.Application.Features.Dispatch.Queries.CheckAreaEligibility;
using Nimpression.Application.Features.Dispatch.Queries.GetJobTaskById;
using Nimpression.Application.Features.Dispatch.Queries.GetJobTasksList;
using Nimpression.Application.Features.Dispatch.Queries.GetUnacknowledgedTaskAlerts;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 任务派发与状态机 Minimal API 端点模块（F5.1–F5.5 / F4.3）。
/// 由 IEndpointModule 自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class DispatchEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dispatch")
            .WithTags("Dispatch");

        // F5.1: 派发任务分页列表查询
        group.MapGet("/tasks", async (
            [FromQuery] Guid? driverId,
            [FromQuery] Guid? vehicleId,
            [FromQuery] Guid? areaId,
            [FromQuery] JobTaskStatus? status,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? searchTerm,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new JobTaskFilter(
                driverId,
                vehicleId,
                areaId,
                status,
                from,
                to,
                searchTerm,
                page ?? 1,
                pageSize ?? 20);

            var result = await sender.Send(new GetJobTasksListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetJobTasksList")
        .WithSummary("获取派发任务列表（支持司机/车辆/区域/状态/时间跨度筛选）");

        // F5.5: 未确认提醒预警列表查询（指派后 30 分钟未确认产出提醒）
        group.MapGet("/tasks/unacknowledged-alerts", async (
            [FromQuery] int? thresholdMinutes,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetUnacknowledgedTaskAlertsQuery(thresholdMinutes ?? 30), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetUnacknowledgedTaskAlerts")
        .WithSummary("获取超过阈值（默认 30 分钟）指派后未确认的任务提醒列表");

        // F4.3: 派单区域资格检查
        group.MapGet("/check-area-eligibility", async (
            [FromQuery] Guid driverId,
            [FromQuery] Guid areaId,
            [FromQuery] DateOnly scheduledDate,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new CheckAreaEligibilityQuery(driverId, areaId, scheduledDate), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("CheckAreaEligibility")
        .WithSummary("检查司机是否被分配到指定区域（若不在区域内返回可越过的警告提示）");

        // F5.1: 按 ID 获取任务详情
        group.MapGet("/tasks/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetJobTaskByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetJobTaskById")
        .WithSummary("按 ID 获取派发任务完整详情");

        // F5.1: 创建任务（Admin / Dispatcher）
        group.MapPost("/tasks", async (
            [FromHeader(Name = "X-Client-Request-Id")] string? headerClientRequestId,
            [FromBody] CreateJobTaskRequest request,
            IIdempotencyService idempotencyService,
            ISender sender,
            CancellationToken ct) =>
        {
            var clientRequestId = !string.IsNullOrWhiteSpace(headerClientRequestId)
                ? headerClientRequestId
                : request.ClientRequestId?.ToString();

            var command = new CreateJobTaskCommand(
                request.Ref,
                request.Title,
                request.AreaId,
                request.ScheduledFor,
                request.Priority ?? TaskPriority.Medium,
                request.Description,
                request.PlannedDistanceKm,
                request.DriverId,
                request.VehicleId,
                request.OverrideAreaWarning ?? false);

            var result = await idempotencyService.ExecuteAsync(
                clientRequestId ?? string.Empty,
                request,
                () => sender.Send(command, ct),
                ct);

            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("CreateJobTask")
        .WithSummary("创建派发任务并可选直接指派司机与车辆");

        // F5.1: 指派任务（Admin / Dispatcher）
        group.MapPost("/tasks/{id:guid}/assign", async (
            Guid id,
            [FromBody] AssignJobTaskRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new AssignJobTaskCommand(
                id,
                request.DriverId,
                request.VehicleId,
                request.ScheduledFor,
                request.OverrideAreaWarning ?? false);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("AssignJobTask")
        .WithSummary("指派任务给指定司机与车辆（跨区域指派需明确确认越过警告）");

        // F5.2 & F5.4: 司机确认任务（Draft -> Assigned -> Acknowledged），支持离线幂等重放
        group.MapPost("/tasks/{id:guid}/acknowledge", async (
            Guid id,
            [FromHeader(Name = "X-Client-Request-Id")] string? headerClientRequestId,
            [FromBody] AcknowledgeJobTaskRequest? request,
            IIdempotencyService idempotencyService,
            ISender sender,
            CancellationToken ct) =>
        {
            var clientRequestId = !string.IsNullOrWhiteSpace(headerClientRequestId)
                ? headerClientRequestId
                : request?.ClientRequestId?.ToString();

            var command = new AcknowledgeJobTaskCommand(id, request?.AcknowledgedAt);
            var payload = new { TaskId = id, AcknowledgedAt = request?.AcknowledgedAt };

            var result = await idempotencyService.ExecuteAsync(
                clientRequestId ?? string.Empty,
                payload,
                () => sender.Send(command, ct),
                ct);

            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("AcknowledgeJobTask")
        .WithSummary("司机确认已接收任务（支持幂等重放，非法跃迁返回 422，越权返回 403）");

        // F5.2 & F5.4: 司机开始执行任务（Acknowledged -> InProgress），支持离线幂等重放
        group.MapPost("/tasks/{id:guid}/start", async (
            Guid id,
            [FromHeader(Name = "X-Client-Request-Id")] string? headerClientRequestId,
            [FromBody] StartJobTaskRequest? request,
            IIdempotencyService idempotencyService,
            ISender sender,
            CancellationToken ct) =>
        {
            var clientRequestId = !string.IsNullOrWhiteSpace(headerClientRequestId)
                ? headerClientRequestId
                : request?.ClientRequestId?.ToString();

            var command = new StartJobTaskCommand(id, request?.StartedAt, request?.StartOdometerKm);
            var payload = new { TaskId = id, StartedAt = request?.StartedAt, StartOdometerKm = request?.StartOdometerKm };

            var result = await idempotencyService.ExecuteAsync(
                clientRequestId ?? string.Empty,
                payload,
                () => sender.Send(command, ct),
                ct);

            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("StartJobTask")
        .WithSummary("司机开始执行任务并记录起始里程表读数（支持幂等重放）");

        // F5.2 & F5.4: 司机完成任务（InProgress -> Completed），支持离线幂等重放
        group.MapPost("/tasks/{id:guid}/complete", async (
            Guid id,
            [FromHeader(Name = "X-Client-Request-Id")] string? headerClientRequestId,
            [FromBody] CompleteJobTaskRequest? request,
            IIdempotencyService idempotencyService,
            ISender sender,
            CancellationToken ct) =>
        {
            var clientRequestId = !string.IsNullOrWhiteSpace(headerClientRequestId)
                ? headerClientRequestId
                : request?.ClientRequestId?.ToString();

            var command = new CompleteJobTaskCommand(
                id,
                request?.CompletedAt,
                request?.ActualDistanceKm,
                request?.EndOdometerKm);
            var payload = new { TaskId = id, CompletedAt = request?.CompletedAt, ActualDistanceKm = request?.ActualDistanceKm, EndOdometerKm = request?.EndOdometerKm };

            var result = await idempotencyService.ExecuteAsync(
                clientRequestId ?? string.Empty,
                payload,
                () => sender.Send(command, ct),
                ct);

            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("CompleteJobTask")
        .WithSummary("完成任务并上报实际行驶距离或终点里程（支持幂等重放）");

        // F5.3: 取消任务
        group.MapPost("/tasks/{id:guid}/cancel", async (
            Guid id,
            [FromBody] CancelJobTaskRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CancelJobTaskCommand(id, request.Reason, request.CancelledAt);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("CancelJobTask")
        .WithSummary("取消派发任务并记录原因");
    }
}

public sealed record CreateJobTaskRequest(
    string? Ref,
    string Title,
    Guid AreaId,
    DateTimeOffset ScheduledFor,
    TaskPriority? Priority = TaskPriority.Medium,
    string? Description = null,
    decimal? PlannedDistanceKm = null,
    Guid? DriverId = null,
    Guid? VehicleId = null,
    bool? OverrideAreaWarning = false,
    Guid? ClientRequestId = null);

public sealed record AssignJobTaskRequest(
    Guid DriverId,
    Guid VehicleId,
    DateTimeOffset? ScheduledFor = null,
    bool? OverrideAreaWarning = false);

public sealed record AcknowledgeJobTaskRequest(
    DateTimeOffset? AcknowledgedAt = null,
    Guid? ClientRequestId = null);

public sealed record StartJobTaskRequest(
    DateTimeOffset? StartedAt = null,
    decimal? StartOdometerKm = null,
    Guid? ClientRequestId = null);

public sealed record CompleteJobTaskRequest(
    DateTimeOffset? CompletedAt = null,
    decimal? ActualDistanceKm = null,
    decimal? EndOdometerKm = null,
    Guid? ClientRequestId = null);

public sealed record CancelJobTaskRequest(
    string Reason,
    DateTimeOffset? CancelledAt = null);
