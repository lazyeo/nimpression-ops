using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Drivers.Commands.CreateDriver;
using Nimpression.Application.Features.Drivers.Commands.DeactivateDriver;
using Nimpression.Application.Features.Drivers.Commands.UpdateDriver;
using Nimpression.Application.Features.Drivers.Commands.UpdateDriverSelfProfile;
using Nimpression.Application.Features.Drivers.Commands.UploadDriverAvatar;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Application.Features.Drivers.Queries.CheckDriverDispatchEligibility;
using Nimpression.Application.Features.Drivers.Queries.GetDriverById;
using Nimpression.Application.Features.Drivers.Queries.GetDriversList;
using Nimpression.Application.Features.Drivers.Queries.GetLicenceAlerts;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 司机与人员管理 Minimal API 端点模块（F2.1–F2.4）。
/// 由 IEndpointModule 自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class DriverEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/drivers")
            .WithTags("Drivers");

        // F2.1: 司机列表分页查询
        group.MapGet("/", async (
            [FromQuery] string? searchTerm,
            [FromQuery] string? name,
            [FromQuery] string? employeeNo,
            [FromQuery] DriverStatus? status,
            [FromQuery] Guid? areaId,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new DriverFilter(
                searchTerm,
                name,
                employeeNo,
                status,
                areaId,
                page ?? 1,
                pageSize ?? 20);

            var result = await sender.Send(new GetDriversListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetDriversList")
        .WithSummary("获取司机分页列表（支持姓名/工号/状态/区域筛选）");

        // F2.3: 驾照到期预警列表
        group.MapGet("/licence-alerts", async (
            [FromQuery] int? daysThreshold,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetLicenceAlertsQuery(daysThreshold ?? 30), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetLicenceAlerts")
        .WithSummary("获取 30 天内驾照即将到期或已到期的司机预警列表");

        // F2.1: 按 ID 获取司机详情
        group.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetDriverByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetDriverById")
        .WithSummary("按 ID 获取司机档案详情");

        // F2.3: 检查司机派单资格
        group.MapGet("/{id:guid}/dispatch-check", async (
            Guid id,
            [FromQuery] DateOnly? referenceDate,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new CheckDriverDispatchEligibilityQuery(id, referenceDate), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("CheckDriverDispatchEligibility")
        .WithSummary("检查司机是否允许被派单（未过期且状态激活）");

        // F2.1: 管理员创建司机
        group.MapPost("/", async (
            [FromBody] CreateDriverRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreateDriverCommand
            {
                DisplayName = request.DisplayName,
                Email = request.Email,
                Password = request.Password,
                EmployeeNo = request.EmployeeNo,
                LicenceClass = request.LicenceClass,
                LicenceExpiry = request.LicenceExpiry,
                HourlyRateAmount = request.HourlyRateAmount,
                HourlyRateCurrency = request.HourlyRateCurrency ?? "NZD",
                PerTripRateAmount = request.PerTripRateAmount,
                PerTripRateCurrency = request.PerTripRateCurrency ?? "NZD",
                PerKmRateAmount = request.PerKmRateAmount,
                PerKmRateCurrency = request.PerKmRateCurrency ?? "NZD",
                Phone = request.Phone,
                Address = request.Address,
                EmergencyContact = request.EmergencyContact,
                HiredOn = request.HiredOn,
                AreaIds = request.AreaIds
            };

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("CreateDriver")
        .WithSummary("创建新司机档案与关联用户账号");

        // F2.1: 管理员修改司机
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateDriverRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new UpdateDriverCommand(
                id,
                request.DisplayName,
                request.LicenceClass,
                request.LicenceExpiry,
                request.HourlyRateAmount,
                request.HourlyRateCurrency ?? "NZD",
                request.PerTripRateAmount,
                request.PerTripRateCurrency ?? "NZD",
                request.PerKmRateAmount,
                request.PerKmRateCurrency ?? "NZD",
                request.Phone,
                request.Address,
                request.EmergencyContact,
                request.Status);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("UpdateDriver")
        .WithSummary("管理员修改司机雇佣档案、费率与合规状态");

        // F2.1: 管理员停用司机
        group.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            [FromBody] DeactivateDriverRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new DeactivateDriverCommand(id, request?.Reason);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("DeactivateDriver")
        .WithSummary("停用司机并触发停用领域事件");

        // F2.2: 头像上传（按 magic bytes 校验，拒绝伪装文件）
        group.MapPost("/{id:guid}/avatar", async (
            Guid id,
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "file_required", message = "No file was uploaded." });
            }

            await using var stream = file.OpenReadStream();
            var command = new UploadDriverAvatarCommand(
                id,
                stream,
                file.FileName,
                file.ContentType,
                file.Length);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .DisableAntiforgery()
        .WithName("UploadDriverAvatar")
        .WithSummary("上传司机头像（服务端校验真实魔数，短时效预签名 URL）");

        // F2.4: 司机个人资料自助修改
        group.MapPut("/{id:guid}/profile", async (
            Guid id,
            [FromBody] UpdateDriverSelfProfileRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new UpdateDriverSelfProfileCommand(
                id,
                request.Phone,
                request.EmergencyContact,
                request.Locale ?? "en-NZ",
                request.Address,
                request.EmployeeNo,
                request.HourlyRate,
                request.PerTripRate,
                request.PerKmRate,
                request.Status,
                request.LicenceExpiry);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("UpdateDriverSelfProfile")
        .WithSummary("司机自助修改手机号/紧急联系人/语言偏好（禁止修改工号/费率/状态，违者 403）");
    }
}

public sealed record CreateDriverRequest(
    string DisplayName,
    string Email,
    string? Password,
    string EmployeeNo,
    string LicenceClass,
    DateOnly LicenceExpiry,
    decimal HourlyRateAmount,
    string? HourlyRateCurrency,
    decimal PerTripRateAmount,
    string? PerTripRateCurrency,
    decimal PerKmRateAmount,
    string? PerKmRateCurrency,
    string Phone,
    string Address,
    string EmergencyContact,
    DateOnly HiredOn,
    List<Guid>? AreaIds);

public sealed record UpdateDriverRequest(
    string DisplayName,
    string LicenceClass,
    DateOnly LicenceExpiry,
    decimal HourlyRateAmount,
    string? HourlyRateCurrency,
    decimal PerTripRateAmount,
    string? PerTripRateCurrency,
    decimal PerKmRateAmount,
    string? PerKmRateCurrency,
    string Phone,
    string Address,
    string EmergencyContact,
    DriverStatus Status);

public sealed record DeactivateDriverRequest(string? Reason);

public sealed record UpdateDriverSelfProfileRequest(
    string Phone,
    string EmergencyContact,
    string? Locale,
    string? Address = null,
    string? EmployeeNo = null,
    decimal? HourlyRate = null,
    decimal? PerTripRate = null,
    decimal? PerKmRate = null,
    DriverStatus? Status = null,
    DateOnly? LicenceExpiry = null);
