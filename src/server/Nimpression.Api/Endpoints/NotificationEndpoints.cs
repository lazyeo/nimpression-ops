using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Notifications.Compliance.Commands.TriggerComplianceScan;
using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Application.Features.Notifications.EmailLogs.Commands.ResendEmail;
using Nimpression.Application.Features.Notifications.EmailLogs.Queries.GetEmailLogById;
using Nimpression.Application.Features.Notifications.EmailLogs.Queries.GetEmailLogsList;
using Nimpression.Application.Features.Notifications.EmailTemplates.Commands.ActivateEmailTemplate;
using Nimpression.Application.Features.Notifications.EmailTemplates.Commands.CreateEmailTemplate;
using Nimpression.Application.Features.Notifications.EmailTemplates.Commands.DeactivateEmailTemplate;
using Nimpression.Application.Features.Notifications.EmailTemplates.Commands.UpdateEmailTemplate;
using Nimpression.Application.Features.Notifications.EmailTemplates.Queries.GetEmailTemplateById;
using Nimpression.Application.Features.Notifications.EmailTemplates.Queries.GetEmailTemplateByKey;
using Nimpression.Application.Features.Notifications.EmailTemplates.Queries.GetEmailTemplatesList;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.ActivatePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.CreatePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.DeactivatePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.DeletePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Commands.UpdatePartnerContact;
using Nimpression.Application.Features.Notifications.PartnerContacts.Queries.GetPartnerContactById;
using Nimpression.Application.Features.Notifications.PartnerContacts.Queries.GetPartnerContactsList;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 邮件与外部集成模块 Minimal API 端点（F11 / W5）。
/// 包含伙伴联系人管理、邮件模板管理、发信日志追踪与合规扫描触发端点。
/// 由 IEndpointModule 自动扫描发现挂载，无需修改 Program.cs。
/// </summary>
public sealed class NotificationEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/notifications")
            .WithTags("Notifications");

        // ── 1. 外部伙伴联系人管理（F11.1）─────────────────────────────
        var partnersGroup = group.MapGroup("/partner-contacts");

        partnersGroup.MapGet("/", async (
            [FromQuery] PartnerKind? kind,
            [FromQuery] bool? active,
            [FromQuery] string? searchTerm,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new PartnerContactFilter(kind, active, searchTerm, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(new GetPartnerContactsListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetPartnerContactsList")
        .WithSummary("获取外部伙伴联系人列表");

        partnersGroup.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPartnerContactByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetPartnerContactById")
        .WithSummary("按 ID 获取外部伙伴联系人详情");

        partnersGroup.MapPost("/", async (
            [FromBody] CreatePartnerContactRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreatePartnerContactCommand(
                request.Kind,
                request.CompanyName,
                request.Email,
                request.Active ?? true);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("CreatePartnerContact")
        .WithSummary("创建外部伙伴联系人（三类伙伴：Insurer/Maintenance/Inspection）");

        partnersGroup.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdatePartnerContactRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new UpdatePartnerContactCommand(
                id,
                request.Kind,
                request.CompanyName,
                request.Email);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("UpdatePartnerContact")
        .WithSummary("更新外部伙伴联系人信息");

        partnersGroup.MapPost("/{id:guid}/activate", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new ActivatePartnerContactCommand(id), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("ActivatePartnerContact")
        .WithSummary("启用外部伙伴联系人");

        partnersGroup.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new DeactivatePartnerContactCommand(id), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("DeactivatePartnerContact")
        .WithSummary("停用外部伙伴联系人（停用后不再接收任何邮件）");

        partnersGroup.MapDelete("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new DeletePartnerContactCommand(id), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("DeletePartnerContact")
        .WithSummary("删除外部伙伴联系人");

        // ── 2. 邮件模板管理（F11.2）─────────────────────────────────
        var templatesGroup = group.MapGroup("/templates");

        templatesGroup.MapGet("/", async (
            [FromQuery] string? searchTerm,
            [FromQuery] bool? active,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new EmailTemplateFilter(searchTerm, active, page ?? 1, pageSize ?? 20);
            var result = await sender.Send(new GetEmailTemplatesListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetEmailTemplatesList")
        .WithSummary("获取邮件模板列表");

        templatesGroup.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEmailTemplateByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetEmailTemplateById")
        .WithSummary("按 ID 获取邮件模板详情");

        templatesGroup.MapGet("/by-key/{key}", async (
            string key,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEmailTemplateByKeyQuery(key), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetEmailTemplateByKey")
        .WithSummary("按 Key 获取邮件模板详情");

        templatesGroup.MapPost("/", async (
            [FromBody] CreateEmailTemplateRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateEmailTemplateCommand(
                request.Key,
                request.SubjectEn,
                request.SubjectZh,
                request.BodyEn,
                request.BodyZh,
                request.Active ?? true);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("CreateEmailTemplate")
        .WithSummary("创建邮件模板（缺失必须占位符时返回 422 报错）");

        templatesGroup.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateEmailTemplateRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new UpdateEmailTemplateCommand(
                id,
                request.SubjectEn,
                request.SubjectZh,
                request.BodyEn,
                request.BodyZh);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateEmailTemplate")
        .WithSummary("更新邮件模板内容（缺失必须占位符时返回 422 报错）");

        templatesGroup.MapPost("/{id:guid}/activate", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new ActivateEmailTemplateCommand(id), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("ActivateEmailTemplate")
        .WithSummary("启用邮件模板");

        templatesGroup.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new DeactivateEmailTemplateCommand(id), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DeactivateEmailTemplate")
        .WithSummary("停用邮件模板");

        // ── 3. 邮件发送日志（F11.5）─────────────────────────────────
        var logsGroup = group.MapGroup("/logs");

        logsGroup.MapGet("/", async (
            [FromQuery] string? status,
            [FromQuery] string? templateKey,
            [FromQuery] string? toAddress,
            [FromQuery] string? correlationId,
            [FromQuery] DateTimeOffset? fromDate,
            [FromQuery] DateTimeOffset? toDate,
            [FromQuery] string? searchTerm,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new EmailLogFilter(
                status, templateKey, toAddress, correlationId,
                fromDate, toDate, searchTerm, page ?? 1, pageSize ?? 20);

            var result = await sender.Send(new GetEmailLogsListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetEmailLogsList")
        .WithSummary("获取邮件发送日志列表（包含状态、重试次数与错误信息）");

        logsGroup.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetEmailLogByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetEmailLogById")
        .WithSummary("按 ID 获取邮件发送日志详情");

        logsGroup.MapPost("/{id:guid}/resend", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new ResendEmailCommand(id), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("ResendEmailLog")
        .WithSummary("手动重发失败邮件");

        // ── 4. 合规到期扫描触发端点（F3.5 / F11）─────────────────────────
        group.MapPost("/compliance/scan", async (
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new TriggerComplianceScanCommand(), ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("TriggerComplianceScan")
        .WithSummary("手动触发车辆合规到期预警扫描（WOF/COF/保险在 30/14/7 天内到期）");
    }
}

public sealed record CreatePartnerContactRequest(
    PartnerKind Kind,
    string CompanyName,
    string Email,
    bool? Active = true);

public sealed record UpdatePartnerContactRequest(
    PartnerKind Kind,
    string CompanyName,
    string Email);

public sealed record CreateEmailTemplateRequest(
    string Key,
    string SubjectEn,
    string SubjectZh,
    string BodyEn,
    string BodyZh,
    bool? Active = true);

public sealed record UpdateEmailTemplateRequest(
    string SubjectEn,
    string SubjectZh,
    string BodyEn,
    string BodyZh);
