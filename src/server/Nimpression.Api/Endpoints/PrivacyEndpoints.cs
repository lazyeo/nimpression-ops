using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Privacy.Commands.AnonymizeDriver;
using Nimpression.Application.Features.Privacy.Commands.ExecuteRetentionCleanup;
using Nimpression.Application.Features.Privacy.Commands.RecordPrivacyConsent;
using Nimpression.Application.Features.Privacy.Queries.ExportPersonalData;
using Nimpression.Application.Features.Privacy.Queries.GetDataClassification;
using Nimpression.Application.Features.Privacy.Queries.GetDataSubjectRequests;
using Nimpression.Application.Features.Privacy.Queries.GetPrivacyConsentStatus;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 隐私合规与数据主权 Minimal API 端点模块（N2 隐私合规）。
/// 由 IEndpointModule 自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class PrivacyEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/privacy")
            .WithTags("Privacy & Data Sovereignty");

        // N2.2: 数据分级分类清单查询
        group.MapGet("/classification", async (
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDataClassificationQuery(), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetDataClassification")
        .WithSummary("查询系统各业务字段的数据敏感度、法律依据与保留期分级清单");

        // N2.3: 执行保留策略清理任务（默认必须为 dry-run）
        group.MapPost("/cleanup", async (
            [FromBody] ExecuteRetentionCleanupRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ExecuteRetentionCleanupCommand(
                request?.ReferenceDate,
                request?.Execute ?? false);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("ExecuteRetentionCleanup")
        .WithSummary("执行过期数据保留策略清理任务（默认 Dry-Run 仅报告；显式 execute=true 时真删）");

        // N2.4: 司机本人自助数据导出（IPP 6 查阅权）
        group.MapGet("/export", async (
            ICurrentUser currentUser,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!currentUser.UserId.HasValue)
            {
                return Results.Unauthorized();
            }

            var query = new ExportPersonalDataQuery(currentUser.UserId.Value);
            var result = await sender.Send(query, ct);

            if (!result.IsSuccess)
            {
                return result.ToHttpResult();
            }

            return Results.File(
                result.Value.ContentBytes,
                result.Value.ContentType,
                result.Value.FileName);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("ExportMyPersonalData")
        .WithSummary("司机自助导出本人的全量业务与个人数据 ZIP 归档包（IPP 6 查阅权）");

        // N2.4: 按指定用户 ID 导出数据（带越权 403 校验）
        group.MapGet("/export/{userId:guid}", async (
            Guid userId,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new ExportPersonalDataQuery(userId);
            var result = await sender.Send(query, ct);

            if (!result.IsSuccess)
            {
                return result.ToHttpResult();
            }

            return Results.File(
                result.Value.ContentBytes,
                result.Value.ContentType,
                result.Value.FileName);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("ExportUserPersonalData")
        .WithSummary("按用户 ID 导出个人数据包（司机越权导出他人数据返回 403）");

        // N2.5: 离职司机数据不可逆匿名化（保留财务与事故法定关联）
        group.MapPost("/anonymize/{driverId:guid}", async (
            Guid driverId,
            [FromBody] AnonymizeDriverRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new AnonymizeDriverCommand(driverId, request?.Reason);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("AnonymizeDriver")
        .WithSummary("对离职司机执行不可逆数据匿名化（保留工资与事故聚合统计不变）");

        // N2.7: 获取当前用户隐私协议同意状态
        group.MapGet("/consent", async (
            [FromQuery] string? version,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetPrivacyConsentStatusQuery(version ?? "2026.1");
            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetPrivacyConsentStatus")
        .WithSummary("获取当前用户在指定版本下的隐私政策告知与同意状态");

        // N2.7: 提交用户隐私政策同意记录
        group.MapPost("/consent", async (
            [FromBody] RecordConsentRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new RecordPrivacyConsentCommand(request?.PolicyVersion ?? "2026.1");
            var result = await sender.Send(command, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("RecordPrivacyConsent")
        .WithSummary("记录用户对隐私政策的签署同意记录与时间戳");

        // 隐私权利工单历史查询
        group.MapGet("/requests", async (
            [FromQuery] Guid? userId,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetDataSubjectRequestsQuery(userId);
            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetDataSubjectRequests")
        .WithSummary("查询数据主体隐私权利请求（查阅/匿名化/更正）处理进度");
    }
}

public sealed record ExecuteRetentionCleanupRequest(
    DateTimeOffset? ReferenceDate = null,
    bool Execute = false);

public sealed record AnonymizeDriverRequest(string? Reason);

public sealed record RecordConsentRequest(string? PolicyVersion = "2026.1");
