using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.Abstractions;
using Nimpression.Application.Features.Payroll.Commands.CalculatePayslipSettlement;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services.Payroll;
using Nimpression.Domain.Common;

namespace Nimpression.Application.Features.Payroll.TaxProfiles;

public sealed class TaxProfileHandlers(ITaxProfileRepository profiles, IPayrollRepository payroll,
    ICurrentUser user, IDateTimeProvider clock, IUnitOfWork unitOfWork, IAuditSink audit,
    IRequestHandler<CalculatePayslipSettlementCommand, Result<PayslipDto>> settlementHandler)
    : IRequestHandler<SubmitTaxProfileCommand, Result<DriverTaxProfileDto>>,
      IRequestHandler<ApproveTaxProfileCommand, Result<DriverTaxProfileDto>>,
      IRequestHandler<CloseTaxProfileCommand, Result<DriverTaxProfileDto>>,
      IRequestHandler<GetTaxProfilesQuery, Result<PagedResult<DriverTaxProfileDto>>>,
      IRequestHandler<GetPayslipTaxProfileQuery, Result<DriverTaxProfileDto?>>,
      IRequestHandler<SettleFromTaxProfileCommand, Result<PayslipDto>>
{
    private static Error Forbidden() => Error.Forbidden("forbidden", "This action is not available for your account.");
    private static Error Missing() => Error.NotFound("tax_profile_not_found", "This tax declaration is no longer available.");
    private static Error NotPending() => Error.Conflict("tax_profile_not_pending", "This declaration has already been reviewed or withdrawn. Refresh its status.");
    private bool ValidDate(DateOnly date) => date >= NzTimeZone.ToNzDateOnly(clock.UtcNow)
        && date >= new DateOnly(2026, 4, 1) && date <= new DateOnly(2027, 3, 31);
    private static Error DateError() => Error.Validation("tax_profile_effective_date_invalid", "Choose today or a future effective date within the supported tax year.");
    private static Error ConfirmationError() => Error.Validation("tax_profile_confirmation_required", "Confirm that these tax details have been checked before submitting.");

    public async Task<Result<DriverTaxProfileDto>> Handle(SubmitTaxProfileCommand request, CancellationToken cancellationToken)
    {
        if (user.Role != UserRole.Driver || user.UserId is null) return Forbidden();
        if (!request.Confirmed) return ConfirmationError();
        if (!ValidDate(request.EffectiveFrom)) return DateError();
        var declaration = request.Declaration;
        if (declaration is null) return Error.Validation("tax_profile_declaration_required", "Complete your tax declaration.");
        var issues = PayrollSettlementCalculator.ValidateDeclaration(request.EffectiveFrom, declaration.WorkerType,
            declaration.Employee?.TaxCode, declaration.Employee?.KiwiSaverEmployee, declaration.Employee is not null, declaration.Contractor);
        if (issues.Count != 0) return Error.Validation(issues[0].Code, issues[0].Message);
        var driver = await payroll.GetDriverByUserIdAsync(user.UserId.Value, cancellationToken);
        if (driver is null) return Forbidden();
        await profiles.LockDriverAsync(driver.Id, cancellationToken);
        if (await profiles.HasPendingAsync(driver.Id, cancellationToken))
            return Error.Conflict("tax_profile_pending_exists", "You already have a tax declaration awaiting review. Withdraw it before submitting a replacement.");
        var profile = new DriverTaxProfile(Guid.NewGuid(), driver.Id, request.EffectiveFrom, declaration, clock.UtcNow, user.UserId.Value);
        profiles.Add(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("DriverTaxProfile", profile.Id, "SubmitTaxProfile", null, null, cancellationToken);
        return await profiles.GetDtoAsync(profile.Id, cancellationToken);
    }

    public async Task<Result<DriverTaxProfileDto>> Handle(ApproveTaxProfileCommand request, CancellationToken cancellationToken)
    {
        if (user.Role != UserRole.Admin || user.UserId is null) return Forbidden();
        if (!request.Confirmed) return ConfirmationError();
        if (!ValidDate(request.EffectiveFrom)) return DateError();
        var profile = await profiles.GetAsync(request.Id, cancellationToken);
        if (profile is null) return Missing();
        await profiles.LockDriverAsync(profile.DriverId, cancellationToken);
        profile = await profiles.GetAsync(request.Id, cancellationToken);
        if (profile is null) return Missing();
        if (profile.Status != DriverTaxProfileStatus.Pending) return NotPending();
        if (request.EffectiveFrom < profile.EffectiveFrom) return DateError();
        if (await profiles.HasApprovedDateAsync(profile.DriverId, request.EffectiveFrom, cancellationToken))
            return Error.Conflict("tax_profile_effective_date_conflict", "An approved tax profile already starts on this date. Choose a later effective date.");
        var personal = profile.Declaration;
        if (personal.WorkerType == SettlementWorkerType.Contractor && (request.KiwiSaverEmployer is not null || request.HolidayPay is not null))
            return Error.Validation("conflicting_profile", "Contractors cannot use employee employer contributions or holiday pay settings.");
        var employee = personal.Employee is { } declared
            ? new EmployeeSettlementProfile(declared.TaxCode, declared.KiwiSaverEmployee, request.KiwiSaverEmployer, request.HolidayPay) : null;
        var check = PayrollSettlementCalculator.Calculate(new(request.EffectiveFrom, SettlementPayFrequency.Weekly, 0,
            personal.WorkerType, employee, personal.Contractor));
        if (!check.IsSuccess) return Error.Validation(check.Issues[0].Code, check.Issues[0].Message);
        profile.Approve(request.EffectiveFrom, request.KiwiSaverEmployer, request.HolidayPay, clock.UtcNow, user.UserId.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("DriverTaxProfile", profile.Id, "ApproveTaxProfile", null, null, cancellationToken);
        return await profiles.GetDtoAsync(profile.Id, cancellationToken);
    }

    public async Task<Result<DriverTaxProfileDto>> Handle(CloseTaxProfileCommand request, CancellationToken cancellationToken)
    {
        if (user.UserId is null || user.Role != (request.Withdraw ? UserRole.Driver : UserRole.Admin)) return Forbidden();
        var profile = await profiles.GetAsync(request.Id, cancellationToken);
        if (profile is null) return Missing();
        if (request.Withdraw)
        {
            var driver = await payroll.GetDriverByUserIdAsync(user.UserId.Value, cancellationToken);
            if (driver?.Id != profile.DriverId) return Forbidden();
        }
        await profiles.LockDriverAsync(profile.DriverId, cancellationToken);
        profile = await profiles.GetAsync(request.Id, cancellationToken);
        if (profile is null) return Missing();
        if (profile.Status != DriverTaxProfileStatus.Pending) return NotPending();
        if (request.Withdraw) profile.Withdraw(clock.UtcNow, user.UserId.Value);
        else profile.Reject(clock.UtcNow, user.UserId.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("DriverTaxProfile", profile.Id, request.Withdraw ? "WithdrawTaxProfile" : "RejectTaxProfile", null, null, cancellationToken);
        return await profiles.GetDtoAsync(profile.Id, cancellationToken);
    }

    public async Task<Result<PagedResult<DriverTaxProfileDto>>> Handle(GetTaxProfilesQuery request, CancellationToken cancellationToken)
    {
        if (user.UserId is null || user.Role != (request.Mine ? UserRole.Driver : UserRole.Admin)) return Forbidden();
        Guid? driverId = request.DriverId;
        if (request.Mine)
        {
            var driver = await payroll.GetDriverByUserIdAsync(user.UserId.Value, cancellationToken);
            if (driver is null) return Forbidden();
            driverId = driver.Id;
        }
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
            return Error.Validation("tax_profile_status_invalid", "Choose a valid declaration status.");
        return await profiles.ListAsync(driverId, request.Status, Math.Clamp(request.Page, 1, 100000), Math.Clamp(request.PageSize, 1, 100), cancellationToken);
    }

    public async Task<Result<DriverTaxProfileDto?>> Handle(GetPayslipTaxProfileQuery request, CancellationToken cancellationToken)
    {
        if (user.Role != UserRole.Admin) return Forbidden();
        var slip = await payroll.GetPayslipByIdAsync(request.PayslipId, cancellationToken);
        if (slip is null) return Error.NotFound("payslip_not_found", "This payslip is no longer available.");
        var profile = await profiles.LatestApprovedAsync(slip.DriverId, request.PayDate, cancellationToken);
        if (profile is null) return Result<DriverTaxProfileDto?>.Success(null);
        return await profiles.GetDtoAsync(profile.Id, cancellationToken);
    }

    public async Task<Result<PayslipDto>> Handle(SettleFromTaxProfileCommand request, CancellationToken cancellationToken)
    {
        if (user.Role != UserRole.Admin) return Forbidden();
        var driverId = await profiles.PayslipDriverIdAsync(request.PayslipId, cancellationToken);
        if (driverId is null) return Error.NotFound("payslip_not_found", "This payslip is no longer available.");
        await profiles.LockDriverAsync(driverId.Value, cancellationToken);
        var profile = await profiles.LatestApprovedAsync(driverId.Value, request.PayDate, cancellationToken);
        if (profile is null || profile.Id != request.ProfileId)
            return Error.Conflict("tax_profile_stale_selection", "The applicable approved tax profile changed or is unavailable. Refresh the selection for this pay date.");
        var settings = new SettlementRequest(request.PayDate, request.Frequency, 0, profile.Declaration.WorkerType,
            profile.ApprovedEmployee, profile.ApprovedContractor);
        // Call the handler within this command's transaction: nested mediator transactions would release the driver lock.
        return await settlementHandler.Handle(new CalculatePayslipSettlementCommand(request.PayslipId, settings, profile.Id), cancellationToken);
    }
}
