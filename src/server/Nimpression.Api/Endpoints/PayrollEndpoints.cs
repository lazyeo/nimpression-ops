using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Payroll.Commands.CalculatePayPeriodPayroll;
using Nimpression.Application.Features.Payroll.Commands.CreatePayPeriod;
using Nimpression.Application.Features.Payroll.Commands.FinalisePayPeriod;
using Nimpression.Application.Features.Payroll.Commands.VoidPayPeriod;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Application.Features.Payroll.Queries.GetDriverPayslips;
using Nimpression.Application.Features.Payroll.Queries.GetPayPeriodById;
using Nimpression.Application.Features.Payroll.Queries.GetPayPeriodPayslips;
using Nimpression.Application.Features.Payroll.Queries.GetPayPeriodsList;
using Nimpression.Application.Features.Payroll.Queries.GetPayslipById;
using Nimpression.Domain.Enums;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// 薪资管理 Minimal API 端点模块（F7.1–F7.12）。
/// 遵循 IEndpointModule 契约，由程序集扫描自动发现与挂载，不修改 Program.cs。
/// </summary>
public sealed class PayrollEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payroll")
            .WithTags("Payroll");

        // F7.7: 创建双周薪期（周一起算，不可重叠）
        group.MapPost("/periods", async (
            [FromBody] CreatePayPeriodRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CreatePayPeriodCommand(request.StartsOn, request.EndsOn);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status201Created);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("CreatePayPeriod")
        .WithSummary("创建薪资周期（双周薪期，周一起算，不可重叠）");

        // 薪资周期列表分页查询
        group.MapGet("/periods", async (
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] PayPeriodStatus? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var filter = new PayPeriodFilter(
                FromDate: fromDate,
                ToDate: toDate,
                Status: status,
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var result = await sender.Send(new GetPayPeriodsListQuery(filter), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetPayPeriodsList")
        .WithSummary("获取薪资周期列表（支持状态与日期筛选）");

        // 按 ID 获取薪资周期详情
        group.MapGet("/periods/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPayPeriodByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetPayPeriodById")
        .WithSummary("按 ID 获取薪资周期详情");

        // F7.8: 试算/计算薪资（试算可重复覆盖已有未定版工资单）
        group.MapPost("/periods/{id:guid}/calculate", async (
            Guid id,
            [FromBody] CalculatePayrollRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new CalculatePayPeriodPayrollCommand(
                PayPeriodId: id,
                DriverId: request?.DriverId,
                PublicHolidays: request?.PublicHolidays is not null ? new HashSet<DateOnly>(request.PublicHolidays) : null,
                MinimumHourlyWage: request?.MinimumHourlyWage);

            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("CalculatePayPeriodPayroll")
        .WithSummary("薪资试算与计算（可重复试算；定版后不可修改）");

        // F7.8: 定版薪资（定版后不可改；F7.6 校验里程差合法性）
        group.MapPost("/periods/{id:guid}/finalise", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new FinalisePayPeriodCommand(id);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("FinalisePayPeriod")
        .WithSummary("定版薪资周期（仅管理员；定版后冻结不可修改，改费率不影响历史）");

        // F7.8: 作废重开薪资周期（必须填理由，全量写审计）
        group.MapPost("/periods/{id:guid}/void", async (
            Guid id,
            [FromBody] VoidPayPeriodRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new VoidPayPeriodCommand(id, request.Reason);
            var result = await sender.Send(command, ct);
            return result.ToHttpResult(StatusCodes.Status200OK);
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly)
        .WithName("VoidPayPeriod")
        .WithSummary("作废已计算/定版的薪资周期并重开（全量写审计）");

        // 获取指定周期内所有工资单列表
        group.MapGet("/periods/{id:guid}/payslips", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPayPeriodPayslipsQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetPayPeriodPayslips")
        .WithSummary("获取指定薪资周期下的全员工资单列表");

        // F7.10: 司机端查询自己的工资单列表
        group.MapGet("/me/payslips", async (
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetDriverPayslipsQuery(
                DriverId: null, // 由 Handler 从当前用户 Token 自动解析 DriverId
                FromDate: fromDate,
                ToDate: toDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetMyPayslips")
        .WithSummary("司机端查询本人已定版工资单历史");

        // 管理端按司机查询工资单历史
        group.MapGet("/drivers/{driverId:guid}/payslips", async (
            Guid driverId,
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetDriverPayslipsQuery(
                DriverId: driverId,
                FromDate: fromDate,
                ToDate: toDate,
                Page: page ?? 1,
                PageSize: pageSize ?? 20);

            var result = await sender.Send(query, ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.Dispatcher)
        .WithName("GetDriverPayslips")
        .WithSummary("管理端查询指定司机的工资单历史");

        // F7.10 / F7.11 / F7.12: 按 ID 查询工资单详情（含双套明细、班次追溯、任务追溯与罚单独立分区展示）
        // 司机查他人 403（不是 404）
        group.MapGet("/payslips/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetPayslipByIdQuery(id), ct);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("GetPayslipById")
        .WithSummary("按 ID 获取工资单详情（司机只能查自己已定版的工资单，查他人返回 403）");
    }
}

public sealed record CreatePayPeriodRequest(
    DateOnly StartsOn,
    DateOnly? EndsOn = null);

public sealed record CalculatePayrollRequest(
    Guid? DriverId = null,
    DateOnly[]? PublicHolidays = null,
    decimal? MinimumHourlyWage = null);

public sealed record VoidPayPeriodRequest(
    string Reason);
