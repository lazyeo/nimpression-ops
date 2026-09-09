using Nimpression.Domain.Entities.Payroll;

namespace Nimpression.Application.Features.Payroll.DTOs;

/// <summary>Driver-facing amounts distinguish calculated gross pay from unavailable deductions.</summary>
public sealed record DriverPayslipDto(
    Guid Id,
    DateOnly PeriodStartsOn,
    DateOnly PeriodEndsOn,
    DateTimeOffset? PayDate,
    decimal GrossPay,
    decimal? NetPay,
    decimal? Deductions,
    string DeductionCalculationStatus,
    decimal TotalHours,
    decimal HourlyRate,
    string Currency,
    PayslipSettlementSnapshot? Settlement)
{
    public static DriverPayslipDto FromPayslip(PayslipDto payslip) => new(
        payslip.Id,
        payslip.PeriodStartsOn,
        payslip.PeriodEndsOn,
        payslip.PaidAt,
        payslip.Settlement?.Calculation.TaxableGross ?? payslip.GrossPay,
        payslip.NetPay,
        payslip.Deductions,
        payslip.SettlementStatus,
        payslip.OrdinaryHours + payslip.OvertimeHours + payslip.HolidayHours,
        payslip.HourlyRateSnapshot,
        payslip.Currency,
        payslip.Settlement);
}
