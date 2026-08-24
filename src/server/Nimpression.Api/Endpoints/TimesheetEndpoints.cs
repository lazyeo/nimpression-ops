using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Timesheets.Commands.AdminCorrectShift;
using Nimpression.Application.Features.Timesheets.Commands.ClockIn;
using Nimpression.Application.Features.Timesheets.Commands.ClockOut;
using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Application.Features.Timesheets.Queries.GetCurrentActiveShift;
using Nimpression.Application.Features.Timesheets.Queries.GetShiftById;
using Nimpression.Application.Features.Timesheets.Queries.GetTimesheetsList;
using Nimpression.Application.Features.Timesheets.Queries.GetTimesheetSummary;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 打卡与工时管理 Minimal API 端点模块（F6.1–F6.5）。
/// 遵循 IEndpointModule 契约，由程序集扫描自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class TimesheetEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/timesheets")
            .WithTags("Timesheets");

        // F6.1: 上班打卡（支持 GPS 降级，未下班再次打卡返回 409）
        group.MapPost("/clock-in", async (
            [FromBody] ClockInRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ClockInCommand(
                request.DriverId,
                request.ClockInAt,
                request.Latitude,
                request.Longitude,
                request.VehicleId,
                request.LocationUnavailable);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("ClockIn")
        .WithSummary("司机上班打卡（记录时间与GPS坐标；可拒绝授权降级为无坐标；未下班重复打卡返回 409）");

        // F6.1: 下班打卡
        group.MapPost("/clock-out", async (
            [FromBody] ClockOutRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ClockOutCommand(
                request.ShiftId,
                request.DriverId,
                request.ClockOutAt,
                request.Latitude,
                request.Longitude,
                request.BreakMinutes ?? 0,
                request.Note,
                request.LocationUnavailable);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("ClockOut")
        .WithSummary("司机下班打卡（记录时间、GPS、休息时长与备注）");

        // 查询当前进行中的活跃班次
        group.MapGet("/active", async (
            [FromQuery] Guid? driverId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCurrentActiveShiftQuery(driverId), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetActiveShift")
        .WithSummary("获取当前进行中的活跃班次");

        // F6.5: 司机端专用本期工时汇总
        group.MapGet("/me/summary", async (
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new TimesheetSummaryFilter(
                DriverId: null, // 由 Handler 从当前用户自动绑定
                FromDate: fromDate,
                ToDate: toDate);

            var result = await sender.Send(new GetTimesheetSummaryQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetDriverMyTimesheetSummary")
        .WithSummary("司机端查询本期工时汇总（与管理端口径完全一致，误差 0）");

        // F6.5: 管理端工时汇总（支持按指定司机或全员聚合）
        group.MapGet("/summary", async (
            [FromQuery] Guid? driverId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new TimesheetSummaryFilter(
                DriverId: driverId,
                FromDate: fromDate,
                ToDate: toDate);

            var result = await sender.Send(new GetTimesheetSummaryQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetTimesheetSummary")
        .WithSummary("管理端或司机查询指定区间工时汇总（与司机端口径完全一致，误差 0）");

        // 班次分页列表查询
        group.MapGet("/", async (
            [FromQuery] Guid? driverId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] ShiftStatus? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new TimesheetFilter(
                DriverId: driverId,
                FromDate: fromDate,
                ToDate: toDate,
                Status: status,
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var result = await sender.Send(new GetTimesheetsListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetTimesheetsList")
        .WithSummary("获取班次打卡历史列表（支持司机/日期/状态筛选）");

        // 按 ID 获取班次详情（F6.2 跨零点工时与归属日验证）
        group.MapGet("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetShiftByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetShiftById")
        .WithSummary("按 ID 获取班次打卡记录详情（含归属上班日与净工时）");

        // F6.4: 管理员更正打卡记录（必须填理由，缺理由 422，原值新值全量入审计）
        group.MapPost("/{id:guid}/admin-correct", async (
            Guid id,
            [FromBody] AdminCorrectShiftRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new AdminCorrectShiftCommand(
                ShiftId: id,
                NewClockInAt: request.NewClockInAt,
                NewClockOutAt: request.NewClockOutAt,
                NewBreakMinutes: request.NewBreakMinutes ?? 0,
                Reason: request.Reason);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("AdminCorrectShift")
        .WithSummary("管理员更正打卡记录（必须填理由，缺理由 422，原值新值全量入审计）");
    }
}

public sealed record ClockInRequest(
    Guid? DriverId = null,
    DateTimeOffset? ClockInAt = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    Guid? VehicleId = null,
    bool LocationUnavailable = false);

public sealed record ClockOutRequest(
    Guid? ShiftId = null,
    Guid? DriverId = null,
    DateTimeOffset? ClockOutAt = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    int? BreakMinutes = 0,
    string? Note = null,
    bool LocationUnavailable = false);

public sealed record AdminCorrectShiftRequest(
    DateTimeOffset NewClockInAt,
    DateTimeOffset? NewClockOutAt,
    int? NewBreakMinutes,
    string Reason);
