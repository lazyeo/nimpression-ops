using System.Text.Json.Serialization;

namespace Nimpression.Domain.Services.Payroll;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SettlementWorkerType { Employee = 1, Contractor = 2 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SettlementPayFrequency { Weekly = 1, Fortnightly = 2, FourWeekly = 3, Monthly = 4 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HolidayPayMode { OrdinaryAccrual = 1, PayAsYouGoEightPercent = 2 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HolidayPayEligibility { GenuineFixedTermUnderTwelveMonths = 1, IrregularWorkPrecludesAnnualHolidays = 2 }

/// <summary>Ordinary earnings in NZD, excluding PAYG holiday pay and contractor GST.
/// Profiles must reflect verified declarations effective on PayDate. This is not a profile inference API.</summary>
public sealed record SettlementRequest(
    DateOnly PayDate,
    SettlementPayFrequency? Frequency,
    decimal GrossEarnings,
    SettlementWorkerType? WorkerType,
    EmployeeSettlementProfile? Employee,
    ContractorSettlementProfile? Contractor);

public sealed record EmployeeSettlementProfile(
    string? TaxCode,
    KiwiSaverEmployeeConfiguration? KiwiSaverEmployee,
    KiwiSaverEmployerConfiguration? KiwiSaverEmployer,
    HolidayPayConfiguration? HolidayPay);

/// <summary>Rate is a fraction, e.g. 0.035. A verified non-contributing status requires explicit zero
/// and ContributionsRequired=false. Nullable decisions preserve omitted JSON fields for validation.</summary>
public sealed record KiwiSaverEmployeeConfiguration(
    decimal? Rate, bool? ContributionsRequired, bool? TemporaryReductionApproved);

/// <summary>Employer contributions are additional to earnings and taxed by ESCT, not salary sacrifice/PAYE.
/// EsctRate is established from prior-year earnings including gross contributions, or the applicable annual estimate.</summary>
public sealed record KiwiSaverEmployerConfiguration(
    decimal? Rate, bool? ContributionsRequired, bool? TemporaryReductionApproved,
    decimal? EsctRate, bool? EsctRateVerified);

/// <summary>OrdinaryAccrual does not calculate leave entitlement or leave taken/final pay.</summary>
public sealed record HolidayPayConfiguration(
    HolidayPayMode? Mode, bool? EligibilityConfirmed, bool? WrittenAgreementConfirmed,
    HolidayPayEligibility? Eligibility = null);

/// <summary>WithholdingRate must be verified, or an explicit verified exemption requires zero.
/// GstRate must explicitly be zero or 0.15; GST is never included in the withholding base.</summary>
public sealed record ContractorSettlementProfile(
    decimal? WithholdingRate, bool? WithholdingVerified, bool? ExemptionVerified,
    decimal? GstRate, bool? GstTreatmentVerified);

public sealed record SettlementValidationIssue(string Code, string Field, string Message);

public sealed record SettlementResult(
    SettlementCalculation? Calculation, IReadOnlyList<SettlementValidationIssue> Issues)
{
    public bool IsSuccess => Calculation is not null && Issues.Count == 0;
}

/// <summary>Paye includes AccEarnersLevy. IncomeTax is the residual after allocating the rounded levy
/// from PAYE (including any IETC reduction); never deduct ACC again from NetPay.
/// EmployerCost is gross cash remuneration plus gross employer KiwiSaver, including contractor GST
/// when payable. It excludes employer ACC work levies and any unmodelled employment costs.</summary>
public sealed record SettlementCalculation(
    string TaxYear,
    decimal BaseGross,
    decimal HolidayPay,
    decimal TaxableGross,
    decimal IncomeTax,
    decimal AccEarnersLevy,
    decimal Paye,
    decimal StudentLoan,
    decimal EmployeeKiwiSaver,
    decimal EmployerKiwiSaverGross,
    decimal Esct,
    decimal EmployerKiwiSaverNet,
    decimal ContractorWithholding,
    decimal Gst,
    decimal NetPay,
    decimal EmployerCost);
