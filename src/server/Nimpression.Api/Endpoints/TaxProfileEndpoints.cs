using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Nimpression.Api.Common;
using Nimpression.Application.Common.Security;
using Nimpression.Application.Features.Payroll.TaxProfiles;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Services.Payroll;

namespace Nimpression.Api.Endpoints;

public sealed class TaxProfileEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payroll").WithTags("Tax profiles");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (DbUpdateConcurrencyException)
            { return Conflict("tax_profile_conflict"); }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
                && postgres.ConstraintName is "UX_DriverTaxProfiles_Pending" or "UX_DriverTaxProfiles_ApprovedDate")
            { return Conflict("tax_profile_conflict"); }
        });
        group.MapGet("/tax-profiles/mine", async (DriverTaxProfileStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetTaxProfilesQuery(true, Status: status, Page: page ?? 1, PageSize: pageSize ?? 50), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.DriverOnly);
        group.MapPost("/tax-profiles/mine", async ([FromBody] SubmitTaxProfileCommand request, ISender sender, CancellationToken ct) =>
            (await sender.Send(request, ct)).ToHttpResult(StatusCodes.Status201Created))
            .RequireAuthorization(AuthorizationPolicies.DriverOnly);
        group.MapPost("/tax-profiles/{id:guid}/withdraw", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CloseTaxProfileCommand(id, true), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.DriverOnly);
        group.MapGet("/tax-profiles", async (Guid? driverId, DriverTaxProfileStatus? status, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetTaxProfilesQuery(false, driverId, status, page ?? 1, pageSize ?? 50), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapPost("/tax-profiles/{id:guid}/approve", async (Guid id, [FromBody] ApproveTaxProfileRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ApproveTaxProfileCommand(id, request.EffectiveFrom, request.KiwiSaverEmployer, request.HolidayPay, request.Confirmed), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapPost("/tax-profiles/{id:guid}/reject", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CloseTaxProfileCommand(id, false), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapGet("/payslips/{id:guid}/tax-profile", async (Guid id, DateOnly payDate, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetPayslipTaxProfileQuery(id, payDate), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
        group.MapPost("/payslips/{id:guid}/settlement-from-profile", async (Guid id, [FromBody] SettlementFromProfileRequest request, ISender sender, CancellationToken ct) =>
            (await sender.Send(new SettleFromTaxProfileCommand(id, request.PayDate, request.Frequency, request.ProfileId), ct)).ToHttpResult())
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);
    }
    private static IResult Conflict(string code) => Results.Problem(statusCode: StatusCodes.Status409Conflict,
        title: code, detail: "These tax settings changed while you were working. Refresh and review them before continuing.");
}

public sealed record ApproveTaxProfileRequest(DateOnly EffectiveFrom, KiwiSaverEmployerConfiguration? KiwiSaverEmployer,
    HolidayPayConfiguration? HolidayPay, bool Confirmed = false);
public sealed record SettlementFromProfileRequest(DateOnly PayDate, SettlementPayFrequency? Frequency, Guid ProfileId);
