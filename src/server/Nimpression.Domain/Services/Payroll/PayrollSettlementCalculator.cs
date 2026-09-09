namespace Nimpression.Domain.Services.Payroll;

/// <summary>Pure settlement of ordinary earnings using IRD 2026/27 v1.01.
/// See SettlementRules.md for sources, rounding, declaration requirements and exclusions.</summary>
public static class PayrollSettlementCalculator
{
    public const string RulesVersion = "IRD-2026-27-v1.01";
    private static readonly HashSet<string> TaxCodes = new(StringComparer.Ordinal)
    {
        "M", "M SL", "ME", "ME SL", "SB", "SB SL", "S", "S SL",
        "SH", "SH SL", "ST", "ST SL", "SA", "SA SL"
    };

    public static SettlementResult Calculate(SettlementRequest request)
    {
        var issues = Validate(request);
        if (issues.Count != 0) return new(null, issues.AsReadOnly());

        if (request.WorkerType == SettlementWorkerType.Contractor)
        {
            var profile = request.Contractor!;
            var withholding = Cents(request.GrossEarnings * profile.WithholdingRate!.Value);
            var gst = decimal.Round(request.GrossEarnings * profile.GstRate!.Value, 2, MidpointRounding.AwayFromZero);
            return Success(new("2026/27", request.GrossEarnings, 0, request.GrossEarnings,
                0, 0, 0, 0, 0, 0, 0, 0, withholding, gst,
                request.GrossEarnings + gst - withholding, request.GrossEarnings + gst));
        }

        var employee = request.Employee!;
        // Round upwards to a cent so the identifiable cash supplement is never below 8%.
        var holiday = employee.HolidayPay!.Mode == HolidayPayMode.PayAsYouGoEightPercent
            ? decimal.Ceiling(request.GrossEarnings * 0.08m * 100m) / 100m : 0m;
        var gross = request.GrossEarnings + holiday;
        var periods = Periods(request.Frequency!.Value);
        var code = employee.TaxCode!;
        var primary = code is "M" or "M SL" or "ME" or "ME SL";
        decimal paye;
        decimal acc;
        if (primary)
        {
            var annual = decimal.Truncate(gross * periods);
            var annualTax = annual switch
            {
                <= 15600m => annual * 0.105m,
                <= 53500m => annual * 0.175m - 1092m,
                <= 78100m => annual * 0.30m - 7779.50m,
                <= 180000m => annual * 0.33m - 10122.50m,
                _ => annual * 0.39m - 20922.50m
            };
            var annualAcc = annual < 156641m ? annual * 0.0175m : 2741.22m;
            var credit = code is "ME" or "ME SL" ? Ietc(annual) : 0m;
            paye = ToPeriod(annualTax + annualAcc - credit, periods);
            acc = ToPeriod(annualAcc, periods);
        }
        else
        {
            var wholeGross = decimal.Truncate(gross);
            var taxRate = code.Split(' ')[0] switch
            {
                "SB" => 0.105m, "S" => 0.175m, "SH" => 0.30m, "ST" => 0.33m, _ => 0.39m
            };
            paye = Cents(wholeGross * (taxRate + 0.0175m));
            acc = Cents(wholeGross * 0.0175m);
        }

        var threshold = primary ? request.Frequency switch
        {
            SettlementPayFrequency.Weekly => 464m,
            SettlementPayFrequency.Fortnightly => 928m,
            SettlementPayFrequency.FourWeekly => 1856m,
            _ => 2010.66m
        } : 0m;
        var studentLoan = code.EndsWith(" SL", StringComparison.Ordinal)
            ? Cents(Math.Max(0, decimal.Truncate(gross) - threshold) * 0.12m) : 0m;
        var employeeKs = Cents(gross * employee.KiwiSaverEmployee!.Rate!.Value);
        var employerKs = Cents(gross * employee.KiwiSaverEmployer!.Rate!.Value);
        var esct = employerKs == 0 ? 0 : Cents(decimal.Truncate(employerKs) * employee.KiwiSaverEmployer.EsctRate!.Value);
        return Success(new("2026/27", request.GrossEarnings, holiday, gross, paye - acc, acc, paye,
            studentLoan, employeeKs, employerKs, esct, employerKs - esct, 0, 0,
            gross - paye - studentLoan - employeeKs, gross + employerKs));
    }

    private static SettlementResult Success(SettlementCalculation calculation) => new(calculation, Array.Empty<SettlementValidationIssue>());
    private static decimal Cents(decimal value) => decimal.Truncate(value * 100m) / 100m;
    private static decimal ToPeriod(decimal annual, int periods) => Cents(Cents(annual / 52m) * 52m / periods);
    private static int Periods(SettlementPayFrequency frequency) => frequency switch
    {
        SettlementPayFrequency.Weekly => 52, SettlementPayFrequency.Fortnightly => 26,
        SettlementPayFrequency.FourWeekly => 13, _ => 12
    };
    private static decimal Ietc(decimal annual) => annual switch
    {
        < 24000m or >= 70000m => 0m,
        <= 66000m => 520m,
        _ => 520m - (annual - 66000m) * 0.13m
    };

    private static List<SettlementValidationIssue> Validate(SettlementRequest request)
    {
        var issues = new List<SettlementValidationIssue>();
        void Add(string code, string field, string message) => issues.Add(new(code, field, message));
        if (request.PayDate < new DateOnly(2026, 4, 1) || request.PayDate > new DateOnly(2027, 3, 31))
            Add("unsupported_tax_year", "PayDate", "Only pay dates in the 2026/27 New Zealand tax year are supported.");
        if (request.Frequency is null || !Enum.IsDefined(request.Frequency.Value))
            Add("unsupported_pay_frequency", "Frequency", "Specify weekly, fortnightly, four-weekly or monthly ordinary pay.");
        // Bound arithmetic and reject non-currency input rather than silently rounding the source earnings.
        if (request.GrossEarnings < 0 || request.GrossEarnings > 1_000_000_000m || Cents(request.GrossEarnings) != request.GrossEarnings)
            Add("invalid_gross_earnings", "GrossEarnings", "Gross earnings must be a non-negative NZD amount in cents, at most one billion dollars.");
        if (request.WorkerType is null || !Enum.IsDefined(request.WorkerType.Value))
            Add("missing_worker_type", "WorkerType", "Specify employee or contractor.");
        else if (request.WorkerType == SettlementWorkerType.Employee)
        {
            if (request.Contractor is not null) Add("conflicting_profile", "Contractor", "An employee cannot use a contractor profile.");
            var employee = request.Employee;
            if (employee is null) { Add("missing_employee_profile", "Employee", "An employee settlement profile is required."); return issues; }
            if (employee.TaxCode is null || !TaxCodes.Contains(employee.TaxCode))
                Add("unsupported_tax_code", "Employee.TaxCode", "Provide a supported ordinary primary or secondary tax code from the employee declaration.");
            var ks = employee.KiwiSaverEmployee;
            if (ks is null || ks.Rate is null || ks.ContributionsRequired is null)
                Add("missing_kiwisaver_configuration", "Employee.KiwiSaverEmployee", "Specify the employee's verified KiwiSaver contribution status and rate.");
            else if ((ks.ContributionsRequired == false && ks.Rate != 0) || (ks.ContributionsRequired == true &&
                     !(ks.Rate is 0.035m or 0.04m or 0.06m or 0.08m or 0.10m || ks.Rate == 0.03m && ks.TemporaryReductionApproved == true)))
                Add("invalid_kiwisaver_rate", "Employee.KiwiSaverEmployee.Rate", "Use an allowed employee rate, approved temporary 3% rate, or explicit zero for a verified non-contributing status.");
            var employer = employee.KiwiSaverEmployer;
            if (employer is null || employer.Rate is null || employer.ContributionsRequired is null)
                Add("missing_employer_configuration", "Employee.KiwiSaverEmployer", "Specify the employer contribution obligation and agreed rate.");
            else
            {
                if (employer.Rate == 0.03m && employer.TemporaryReductionApproved is null)
                    Add("missing_employer_reduction_declaration", "Employee.KiwiSaverEmployer.TemporaryReductionApproved", "Specify whether a temporary contribution reduction has been approved.");
                if (employer.Rate < 0 || employer.Rate > 1 || employer.ContributionsRequired == true && employer.Rate < (employer.TemporaryReductionApproved == true ? 0.03m : 0.035m))
                    Add("invalid_employer_rate", "Employee.KiwiSaverEmployer.Rate", "The employer rate must meet the applicable minimum when compulsory and be between zero and one.");
                if (employer.Rate > 0 && (employer.EsctRateVerified != true || employer.EsctRate is not (0.105m or 0.175m or 0.30m or 0.33m or 0.39m)))
                    Add("missing_verified_esct_rate", "Employee.KiwiSaverEmployer.EsctRate", "A verified statutory ESCT rate is required for employer contributions.");
            }
            var holiday = employee.HolidayPay;
            if (holiday?.Mode is null || !Enum.IsDefined(holiday.Mode.Value))
                Add("missing_holiday_configuration", "Employee.HolidayPay", "Specify ordinary annual holidays or eligible 8% pay-as-you-go holiday pay.");
            else if (holiday.Mode == HolidayPayMode.PayAsYouGoEightPercent && (holiday.EligibilityConfirmed != true || holiday.WrittenAgreementConfirmed != true || holiday.Eligibility is null || !Enum.IsDefined(holiday.Eligibility.Value)))
                Add("holiday_pay_ineligible", "Employee.HolidayPay", "8% holiday pay requires a verified qualifying work arrangement and agreement in the employment agreement.");
        }
        else
        {
            if (request.Employee is not null) Add("conflicting_profile", "Employee", "Contractors cannot use employee deductions or employee holiday pay.");
            var contractor = request.Contractor;
            if (contractor is null) { Add("missing_contractor_profile", "Contractor", "A verified contractor withholding and GST profile is required."); return issues; }
            if (contractor.WithholdingRate is null || contractor.WithholdingRate < 0 || contractor.WithholdingRate > 1 ||
                (contractor.ExemptionVerified is null || contractor.WithholdingVerified is null ||
                 (contractor.ExemptionVerified == true ? contractor.WithholdingRate != 0 : contractor.WithholdingVerified != true)))
                Add("missing_verified_withholding", "Contractor.WithholdingRate", "Specify a verified withholding rate, or a verified exemption with an explicit zero rate.");
            if (contractor.GstTreatmentVerified != true || contractor.GstRate is not (0m or 0.15m))
                Add("missing_verified_gst", "Contractor.GstRate", "Specify verified GST treatment: zero or standard 15%.");
        }
        return issues;
    }
}
