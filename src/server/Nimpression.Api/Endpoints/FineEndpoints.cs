using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Fines.Commands.AcceptFine;
using Nimpression.Application.Features.Fines.Commands.DisputeFine;
using Nimpression.Application.Features.Fines.Commands.StartFineReview;
using Nimpression.Application.Features.Fines.Commands.SubmitFine;
using Nimpression.Application.Features.Fines.Commands.WaiveFine;
using Nimpression.Application.Features.Fines.DTOs;
using Nimpression.Application.Features.Fines.Queries.GetFineById;
using Nimpression.Application.Features.Fines.Queries.GetFinePhotoUrl;
using Nimpression.Application.Features.Fines.Queries.GetFinesList;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 交通罚单模块 Minimal API 端点（F8 罚单）。
/// 由 IEndpointModule 自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class FineEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/fines")
            .WithTags("Fines");

        // F8.1: 提交罚单（司机为自己提交 / 管理员代提交）
        group.MapPost("/", async (
            [FromBody] SubmitFineRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new SubmitFineCommand(
                request.DriverId,
                request.VehicleId,
                request.IssuedOn,
                request.Authority,
                request.Reference,
                request.Amount,
                request.Currency ?? "NZD",
                request.Reason,
                request.TicketPhotoKey);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("SubmitFine")
        .WithSummary("提交交通罚单记录");

        // F8.1: 罚单照片上传
        group.MapPost("/upload-photo", async (
            IFormFile file,
            IObjectStorageService storageService,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "file_required", message = "No file was uploaded." });
            }

            var ext = Path.GetExtension(file.FileName) switch
            {
                { Length: > 0 } e => e.ToLowerInvariant(),
                _ => ".jpg"
            };

            var key = $"fines/{Guid.NewGuid():N}{ext}";
            await using var stream = file.OpenReadStream();
            var uploadedKey = await storageService.UploadAsync(
                "nimpression-media",
                key,
                stream,
                file.ContentType,
                ct);

            return Results.Ok(new { photoKey = uploadedKey });
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .DisableAntiforgery()
        .WithName("UploadFinePhoto")
        .WithSummary("上传罚单照片至对象存储并返回存储 Key");

        // F8.1 / F8.2: 罚单分页列表查询（司机只能看自己的，管理端可按司机/车辆/状态/日期筛选）
        group.MapGet("/", async (
            [FromQuery] Guid? driverId,
            [FromQuery] Guid? vehicleId,
            [FromQuery] FineStatus? status,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] string? searchTerm,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new FineFilter(
                driverId,
                vehicleId,
                status,
                fromDate,
                toDate,
                searchTerm,
                page ?? 1,
                pageSize ?? 20);

            var result = await sender.Send(new GetFinesListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetFinesList")
        .WithSummary("获取交通罚单分页列表");

        // F8.1 / F8.4: 按 ID 获取罚单详情（包含照片短时效预签名 URL）
        group.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFineByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetFineById")
        .WithSummary("按 ID 获取交通罚单详情（越权访问返回 403）");

        // F8.4: 获取罚单照片短时效预签名 URL（越权访问必须 403）
        group.MapGet("/{id:guid}/photo", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetFinePhotoUrlQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(new { url = result.Value })
                : result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetFinePhotoUrl")
        .WithSummary("获取罚单照片预签名 URL（≤15min，越权返回 403）");

        // F8.2: 开始审核（Submitted -> UnderReview）
        group.MapPost("/{id:guid}/start-review", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new StartFineReviewCommand(id), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("StartFineReview")
        .WithSummary("管理员/调度员开始审核罚单（状态流转为 UnderReview）");

        // F8.2 / F8.3: 接受罚单（UnderReview -> Accepted，触发 FineAccepted 领域事件）
        group.MapPost("/{id:guid}/accept", async (
            Guid id,
            [FromBody] AcceptFineRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new AcceptFineCommand(id, request?.ReviewNote), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("AcceptFine")
        .WithSummary("管理员/调度员接受罚单（状态置为 Accepted，发领域事件）");

        // F8.2: 争议罚单（UnderReview -> Disputed）
        group.MapPost("/{id:guid}/dispute", async (
            Guid id,
            [FromBody] DisputeFineRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new DisputeFineCommand(id, request.ReviewNote), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("DisputeFine")
        .WithSummary("管理员/调度员争议罚单（状态置为 Disputed，必须附理由）");

        // F8.2: 减免/撤销罚单（UnderReview -> Waived）
        group.MapPost("/{id:guid}/waive", async (
            Guid id,
            [FromBody] WaiveFineRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new WaiveFineCommand(id, request.ReviewNote), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("WaiveFine")
        .WithSummary("管理员/调度员减免/撤销罚单（状态置为 Waived，必须附理由）");
    }
}

public sealed record SubmitFineRequest(
    Guid? DriverId,
    Guid VehicleId,
    DateOnly IssuedOn,
    string Authority,
    string Reference,
    decimal Amount,
    string? Currency,
    string Reason,
    string? TicketPhotoKey = null);

public sealed record AcceptFineRequest(string? ReviewNote = null);

public sealed record DisputeFineRequest(string ReviewNote);

public sealed record WaiveFineRequest(string ReviewNote);
