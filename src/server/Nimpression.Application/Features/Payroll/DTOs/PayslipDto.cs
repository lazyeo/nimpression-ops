using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.DTOs;

public sealed record PayslipDto(
    Guid Id,
    Guid PayPeriodId,
    DateOnly PeriodStartsOn,
    DateOnly PeriodEndsOn,
    Guid DriverId,
    string? DriverName,
    string? EmployeeNo,
    decimal OrdinaryHours,
    decimal OvertimeHours,
    decimal HolidayHours,
    decimal HourlyRateSnapshot,
    decimal HoursBasedGross,
    int CompletedTripCount,
    decimal TotalDistanceKm,
    decimal PerTripRateSnapshot,
    decimal PerKmRateSnapshot,
    decimal TripBasedGross,
    PayBasis BasisUsed,
    decimal GrossPay,
    string Currency,
    bool MinimumWageTopUp,
    DateTimeOffset CalculatedAt,
    DateTimeOffset? FinalisedAt,
    IReadOnlyList<PayslipLineDto> Lines,
    IReadOnlyList<PayslipShiftDetailDto> ShiftDetails,
    IReadOnlyList<PayslipTripDetailDto> TripDetails,
    IReadOnlyList<PayslipFineDto> Fines,
    string FinesLegalNotice)
{
    public const string DefaultFinesLegalNotice =
        "Under the Wages Protection Act 1983, employer deductions from pay are unlawful without prior written consent. Fines are tracked separately and not deducted from payroll.";

    public static PayslipDto FromEntity(
        Payslip payslip,
        DateOnly startsOn,
        DateOnly endsOn,
        string? driverName = null,
        string? employeeNo = null,
        IReadOnlyList<PayslipShiftDetailDto>? shiftDetails = null,
        IReadOnlyList<PayslipTripDetailDto>? tripDetails = null,
        IReadOnlyList<PayslipFineDto>? fines = null)
    {
        return new PayslipDto(
            Id: payslip.Id,
            PayPeriodId: payslip.PayPeriodId,
            PeriodStartsOn: startsOn,
            PeriodEndsOn: endsOn,
            DriverId: payslip.DriverId,
            DriverName: driverName,
            EmployeeNo: employeeNo,
            OrdinaryHours: payslip.OrdinaryHours.Value,
            OvertimeHours: payslip.OvertimeHours.Value,
            HolidayHours: payslip.HolidayHours.Value,
            HourlyRateSnapshot: payslip.HourlyRateSnapshot.Amount,
            HoursBasedGross: payslip.HoursBasedGross.Amount,
            CompletedTripCount: payslip.CompletedTripCount,
            TotalDistanceKm: payslip.TotalDistanceKm.Value,
            PerTripRateSnapshot: payslip.PerTripRateSnapshot.Amount,
            PerKmRateSnapshot: payslip.PerKmRateSnapshot.Amount,
            TripBasedGross: payslip.TripBasedGross.Amount,
            BasisUsed: payslip.BasisUsed,
            GrossPay: payslip.GrossPay.Amount,
            Currency: payslip.GrossPay.Currency,
            MinimumWageTopUp: payslip.MinimumWageTopUp,
            CalculatedAt: payslip.CalculatedAt,
            FinalisedAt: payslip.FinalisedAt,
            Lines: payslip.Lines.Select(PayslipLineDto.FromEntity).ToList(),
            ShiftDetails: shiftDetails ?? [],
            TripDetails: tripDetails ?? [],
            Fines: fines ?? [],
            FinesLegalNotice: DefaultFinesLegalNotice);
    }
}
