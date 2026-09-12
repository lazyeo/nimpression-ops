using System.Text.Json.Serialization;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.Abstractions;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services.Payroll;

namespace Nimpression.Application.Features.Payroll.Commands.CalculatePayslipSettlement;

public sealed record CalculatePayslipSettlementCommand(Guid PayslipId, [property: JsonIgnore] SettlementRequest Settings, Guid? TaxProfileId = null)
    : IRequest<Result<PayslipDto>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Payslip";
    public Guid? AuditEntityId => PayslipId;
    public string AuditAction => "CalculatePayslipSettlement";
}

public sealed class CalculatePayslipSettlementCommandHandler(
    IPayrollRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditSink auditSink,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<CalculatePayslipSettlementCommand, Result<PayslipDto>>
{
    public async Task<Result<PayslipDto>> Handle(CalculatePayslipSettlementCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
            return Error.Forbidden("forbidden", "Only administrators can calculate payslip settlement.");
        if (request.Settings is null)
            return Error.Validation("payroll_settings_required", "Provide payroll settings before calculating settlement.");

        // Lock the common parent before loading mutable payslips, including when another
        // command is adding a driver's payslip or replacing an existing calculation.
        var period = await repository.GetPayPeriodForPayslipForUpdateAsync(request.PayslipId, cancellationToken);
        if (period is null)
            return Error.NotFound("payslip_not_found", "This payslip is no longer available.");
        var payslip = await repository.GetPayslipByIdAsync(request.PayslipId, cancellationToken);
        if (payslip is null)
            return Error.NotFound("payslip_not_found", "This payslip is no longer available.");
        if (payslip.FinalisedAt.HasValue || period.Status is PayPeriodStatus.Finalised or PayPeriodStatus.Paid)
            return Error.Unprocessable("payslip_finalised", "A finalised payslip cannot be recalculated.");
        if (request.Settings.PayDate < period.EndsOn)
            return Error.Unprocessable("payroll_pay_date_invalid", "The pay date must be on or after the end of this pay period.");
        if (payslip.GrossPay.Currency != "NZD")
            return Error.Unprocessable("payroll_currency_unsupported", "Settlement supports NZD earnings only.");

        var periodDays = period.EndsOn.DayNumber - period.StartsOn.DayNumber + 1;
        var frequencyMatches = request.Settings.Frequency switch
        {
            SettlementPayFrequency.Weekly => periodDays == 7,
            SettlementPayFrequency.Fortnightly => periodDays == 14,
            SettlementPayFrequency.FourWeekly => periodDays == 28,
            SettlementPayFrequency.Monthly => period.StartsOn.Day == 1 && period.EndsOn == period.StartsOn.AddMonths(1).AddDays(-1),
            _ => false
        };
        if (!frequencyMatches)
            return Error.Unprocessable("unsupported_pay_frequency", "Select a pay frequency matching the complete pay period. Irregular or partial periods are not supported.");

        if (request.Settings.WorkerType == SettlementWorkerType.Employee)
        {
            Money minimumRate;
            try { minimumRate = NzAdultMinimumWage.ForPeriod(period.StartsOn, period.EndsOn); }
            catch (DomainValidationException)
            {
                return Error.Unprocessable("payroll_minimum_rules_unsupported", "This period is outside the supported minimum wage rules or crosses a rate change. Review the period before calculating settlement.");
            }
            var hours = payslip.OrdinaryHours.Value + payslip.OvertimeHours.Value + payslip.HolidayHours.Value;
            var minimumGross = new Money(hours * minimumRate.Amount);
            if (payslip.GrossPay.Amount < minimumGross.Amount)
                return Error.Unprocessable("payroll_gross_recalculation_required", "Recalculate gross earnings using the applicable period rules before calculating deductions.");
        }

        // Earnings are trusted persisted amounts, never the client-supplied gross.
        // Contractor remuneration follows the agreed dual basis without the employee wage floor.
        var authoritativeGross = request.Settings.WorkerType == SettlementWorkerType.Contractor
            ? Math.Max(payslip.HoursBasedGross.Amount, payslip.TripBasedGross.Amount)
            : payslip.GrossPay.Amount;
        var settings = request.Settings with { GrossEarnings = authoritativeGross };
        var result = PayrollSettlementCalculator.Calculate(settings);
        if (!result.IsSuccess)
        {
            var issue = result.Issues[0];
            return Error.Unprocessable(issue.Code, issue.Message);
        }

        payslip.SetSettlement(new PayslipSettlementSnapshot(settings, result.Calculation!, PayrollSettlementCalculator.RulesVersion, dateTimeProvider.UtcNow, TaxProfileId: request.TaxProfileId));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditSink.RecordAsync("Payslip", payslip.Id, "CalculatePayslipSettlement", null,
            $"{{\"rulesVersion\":\"{PayrollSettlementCalculator.RulesVersion}\"}}", cancellationToken);

        var driver = await repository.GetDriverByIdAsync(payslip.DriverId, cancellationToken);
        return PayslipDto.FromEntity(payslip, period.StartsOn, period.EndsOn,
            await repository.GetDriverDisplayNameAsync(payslip.DriverId, cancellationToken), driver?.EmployeeNo, period.PaidAt);
    }
}
