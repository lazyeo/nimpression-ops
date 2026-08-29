using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Drivers.Storage;
using Nimpression.Application.Features.Incidents.Commands.ReportIncident;
using Nimpression.Application.Features.Incidents.DTOs;
using Nimpression.Application.Features.Incidents.Queries.GetIncidentById;
using Nimpression.Application.Features.Incidents.Queries.GetIncidentsList;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 事故管理模块 Minimal API 端点（F9 事故）。
/// 由 IEndpointModule 自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class IncidentEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/incidents")
            .WithTags("Incidents");

        // F9.1 / F9.2 / F9.3: 事故上报（司机/管理员均可提交，严重度 ≥ Moderate 自动通知保险方）
        group.MapPost("/", async (
            [FromBody] ReportIncidentRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ReportIncidentCommand(
                request.DriverId,
                request.VehicleId,
                request.OccurredAt,
                request.Location,
                request.Severity,
                request.Description,
                request.PhotoKeys,
                request.ThirdPartyInfo);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("ReportIncident")
        .WithSummary("上报事故报告（司机/管理端均可提交）");

        // F9.1: 事故现场多图上传
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

            var key = $"incidents/{Guid.NewGuid():N}{ext}";
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
        .WithName("UploadIncidentPhoto")
        .WithSummary("上传事故现场照片至对象存储并返回存储 Key");

        // F9.4: 按车辆/司机/时间范围/严重度查询历史事故列表（用于理赔）
        group.MapGet("/", async (
            [FromQuery] Guid? driverId,
            [FromQuery] Guid? vehicleId,
            [FromQuery] IncidentSeverity? severity,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            [FromQuery] string? searchTerm,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new IncidentFilter(
                driverId,
                vehicleId,
                severity,
                fromDate,
                toDate,
                searchTerm,
                page ?? 1,
                pageSize ?? 20);

            var result = await sender.Send(new GetIncidentsListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetIncidentsList")
        .WithSummary("查询事故历史列表（支持按车辆/司机/时间范围/严重度筛选）");

        // F9.1 / F9.4: 按 ID 获取事故详情
        group.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetIncidentByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetIncidentById")
        .WithSummary("按 ID 获取事故报告详情（含照片预签名 URL）");
    }
}

public sealed record ReportIncidentRequest(
    Guid? DriverId,
    Guid VehicleId,
    DateTimeOffset OccurredAt,
    string Location,
    IncidentSeverity Severity,
    string Description,
    List<string>? PhotoKeys = null,
    string? ThirdPartyInfo = null);
