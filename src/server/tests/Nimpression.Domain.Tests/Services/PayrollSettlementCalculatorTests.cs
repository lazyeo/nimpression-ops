using Nimpression.Domain.Services.Payroll;
using System.Text.Json;
using Xunit;

namespace Nimpression.Domain.Tests.Services;

public class PayrollSettlementCalculatorTests
{
    private static SettlementRequest Employee(decimal gross = 1000m, string code = "M",
        SettlementPayFrequency frequency = SettlementPayFrequency.Weekly) =>
        new(new(2026, 9, 9), frequency, gross, SettlementWorkerType.Employee,
            new(code, new(0.035m, true, false), new(0.035m, true, false, 0.175m, true),
                new(HolidayPayMode.OrdinaryAccrual, false, false)), null);

    [Fact]
    public void IrdExample_OrdinaryFourWeeklySalary_HasPublishedPayeStudentLoanAndKiwiSaver()
    {
        // IRD 2026/27 spec p40, example4: ordinary salary excludes the untaxed ESS benefit.
        var actual = PayrollSettlementCalculator.Calculate(Employee(3500m, "M SL", SettlementPayFrequency.FourWeekly)).Calculation!;
        Assert.Equal(589.72m, actual.Paye);
        Assert.Equal(197.28m, actual.StudentLoan);
        Assert.Equal(122.50m, actual.EmployeeKiwiSaver);
        Assert.Equal(2590.50m, actual.NetPay);
        Assert.Equal(actual.Paye, actual.IncomeTax + actual.AccEarnersLevy);
        Assert.Equal(actual.TaxableGross, actual.NetPay + actual.Paye + actual.StudentLoan + actual.EmployeeKiwiSaver);
    }

    [Theory]
    [InlineData("M", 171.50, 0)]
    [InlineData("M SL", 171.50, 64.32)]
    [InlineData("ME", 161.50, 0)]
    [InlineData("ME SL", 161.50, 64.32)]
    [InlineData("SB", 122.50, 0)]
    [InlineData("SB SL", 122.50, 120)]
    [InlineData("S", 192.50, 0)]
    [InlineData("S SL", 192.50, 120)]
    [InlineData("SH", 317.50, 0)]
    [InlineData("SH SL", 317.50, 120)]
    [InlineData("ST", 347.50, 0)]
    [InlineData("ST SL", 347.50, 120)]
    [InlineData("SA", 407.50, 0)]
    [InlineData("SA SL", 407.50, 120)]
    public void OrdinaryTaxCodes_UsePublishedRates(string code, decimal paye, decimal studentLoan)
    {
        // Independent IR340 April2026 p30: weekly1,000 M171.50, ME161.50, SL64.32.
        // Secondary columns use the corresponding published section5.6 flat rates.
        var actual = PayrollSettlementCalculator.Calculate(Employee(1000m, code)).Calculation!;
        Assert.Equal(paye, actual.Paye);
        Assert.Equal(studentLoan, actual.StudentLoan);
    }

    [Theory]
    [InlineData(500.03, 75.25)]
    [InlineData(500.99, 75.43)]
    public void PrimaryPaye_UsesCurrentAccAndAnnualWholeDollars(decimal gross, decimal paye)
    {
        Assert.Equal(paye, PayrollSettlementCalculator.Calculate(Employee(gross)).Calculation!.Paye);
    }

    [Fact]
    public void MonthlyPaye_TruncatesWeeklyBeforeConverting_StudentLoanUsesMonthlyThreshold()
    {
        var actual = PayrollSettlementCalculator.Calculate(Employee(3500.99m, "M SL", SettlementPayFrequency.Monthly)).Calculation!;
        // Annual42011, tax6259.925 + levy735.1925 =6995.1175; weekly134.52 -> monthly582.92.
        Assert.Equal(582.92m, actual.Paye);
        Assert.Equal(178.72m, actual.StudentLoan); // (3500 - 2010.66)*12%, truncated
    }

    [Theory]
    [InlineData(464.99, 0)]
    [InlineData(465.01, 0.12)]
    public void StudentLoan_TruncatesGrossBeforeThreshold(decimal gross, decimal expected)
    {
        Assert.Equal(expected, PayrollSettlementCalculator.Calculate(Employee(gross, "M SL")).Calculation!.StudentLoan);
    }

    [Fact]
    public void PrimaryAcc_CapsAnnualisedLevy_SecondaryRetainsPublishedFlatRate()
    {
        var primary = PayrollSettlementCalculator.Calculate(Employee(10000m)).Calculation!;
        var secondary = PayrollSettlementCalculator.Calculate(Employee(10000m, "SA")).Calculation!;
        Assert.Equal(52.71m, primary.AccEarnersLevy); // 2741.22 /52, truncated
        Assert.Equal(175m, secondary.AccEarnersLevy);
        Assert.Equal(4075m, secondary.Paye);
    }

    [Theory]
    [InlineData(1999.99, false)]
    [InlineData(2000, true)]
    [InlineData(5500, true)]
    [InlineData(5833.34, false)]
    public void Ietc_OnlyAppliesInsideAnnualEligibilityBand(decimal gross, bool hasCredit)
    {
        var m = PayrollSettlementCalculator.Calculate(Employee(gross, "M", SettlementPayFrequency.Monthly)).Calculation!;
        var me = PayrollSettlementCalculator.Calculate(Employee(gross, "ME", SettlementPayFrequency.Monthly)).Calculation!;
        Assert.Equal(hasCredit, me.Paye < m.Paye);
    }

    [Fact]
    public void EmployerEsct_TruncatesContributionToWholeDollars_ThenTaxToCents()
    {
        var actual = PayrollSettlementCalculator.Calculate(Employee(861.85m) with
        {
            Employee = Employee().Employee! with { KiwiSaverEmployer = new(0.06m, true, false, 0.175m, true) }
        }).Calculation!;
        Assert.Equal(51.71m, actual.EmployerKiwiSaverGross); // IRD5.20.6 complete dollars and cents
        Assert.Equal(8.92m, actual.Esct); // floor(51*17.5%, cents)
        Assert.Equal(42.79m, actual.EmployerKiwiSaverNet);
        Assert.Equal(913.56m, actual.EmployerCost);
    }

    [Fact]
    public void EmploymentNzHemiExample_AddsIdentifiableEightPercentToTaxableGross()
    {
        var request = Employee(862.50m, frequency: SettlementPayFrequency.Fortnightly);
        request = request with { Employee = request.Employee! with { HolidayPay = new(
            HolidayPayMode.PayAsYouGoEightPercent, true, true, HolidayPayEligibility.GenuineFixedTermUnderTwelveMonths) } };
        var actual = PayrollSettlementCalculator.Calculate(request).Calculation!;
        Assert.Equal(69m, actual.HolidayPay);
        Assert.Equal(931.50m, actual.TaxableGross);
        Assert.Equal(32.60m, actual.EmployeeKiwiSaver);
        Assert.Equal(0, PayrollSettlementCalculator.Calculate(Employee(862.50m)).Calculation!.HolidayPay);
    }

    [Theory]
    [InlineData(false, true, HolidayPayEligibility.GenuineFixedTermUnderTwelveMonths)]
    [InlineData(true, false, HolidayPayEligibility.GenuineFixedTermUnderTwelveMonths)]
    [InlineData(true, true, null)]
    public void HolidayPay_RequiresActualEligibilityBasisAndAgreement(bool eligible, bool agreed, HolidayPayEligibility? basis)
    {
        var request = Employee();
        request = request with { Employee = request.Employee! with { HolidayPay = new(HolidayPayMode.PayAsYouGoEightPercent, eligible, agreed, basis) } };
        AssertIssue(request, "holiday_pay_ineligible");
    }

    [Fact]
    public void HolidaySupplement_NeverFallsBelowEightPercent_WhenFractionalCent()
    {
        var request = Employee(1.01m);
        request = request with { Employee = request.Employee! with { HolidayPay = new(
            HolidayPayMode.PayAsYouGoEightPercent, true, true, HolidayPayEligibility.IrregularWorkPrecludesAnnualHolidays) } };
        Assert.Equal(0.09m, PayrollSettlementCalculator.Calculate(request).Calculation!.HolidayPay);
    }

    [Fact]
    public void IrdSchedularExample_GstIsOutsideWithholdingBase_AndNoEmployeeDeductions()
    {
        // IRD2026/27 p69: invoice115 inclGST, tax20 on100; recipient gets95.
        var actual = PayrollSettlementCalculator.Calculate(Contractor()).Calculation!;
        Assert.Equal(100m, actual.TaxableGross);
        Assert.Equal(15m, actual.Gst);
        Assert.Equal(20m, actual.ContractorWithholding);
        Assert.Equal(95m, actual.NetPay);
        Assert.Equal(115m, actual.EmployerCost);
        Assert.Equal(0, actual.Paye + actual.AccEarnersLevy + actual.StudentLoan + actual.EmployeeKiwiSaver + actual.EmployerKiwiSaverGross + actual.HolidayPay);
    }

    [Fact]
    public void Contractor_ExplicitExemptionAndZeroGstSupported_NoUndeclaredZeroFallback()
    {
        var request = Contractor() with { Contractor = new(0, false, true, 0, true) };
        Assert.Equal(100m, PayrollSettlementCalculator.Calculate(request).Calculation!.NetPay);
        AssertIssue(request with { Contractor = new(null, false, false, 0, true) }, "missing_verified_withholding");
        AssertIssue(request with { Contractor = new(0.20m, false, false, 0, true) }, "missing_verified_withholding");
        AssertIssue(request with { Contractor = new(0, false, true, null, false) }, "missing_verified_gst");
        AssertIssue(request with { Employee = Employee().Employee }, "conflicting_profile");
    }

    [Fact]
    public void MissingAndUnsupportedInputs_BlockCalculation()
    {
        AssertIssue(Employee() with { Employee = null }, "missing_employee_profile");
        AssertIssue(Contractor() with { Contractor = null }, "missing_contractor_profile");
        AssertIssue(Employee() with { WorkerType = null }, "missing_worker_type");
        AssertIssue(Employee() with { Frequency = null }, "unsupported_pay_frequency");
        AssertIssue(Employee() with { GrossEarnings = -1 }, "invalid_gross_earnings");
        AssertIssue(Employee() with { GrossEarnings = 1.001m }, "invalid_gross_earnings");
        AssertIssue(Employee() with { GrossEarnings = decimal.MaxValue }, "invalid_gross_earnings");
        AssertIssue(Employee() with { Employee = Employee().Employee! with { TaxCode = "STC" } }, "unsupported_tax_code");
        AssertIssue(Employee() with { Employee = Employee().Employee! with { KiwiSaverEmployee = null } }, "missing_kiwisaver_configuration");
        AssertIssue(Employee() with { Employee = Employee().Employee! with { KiwiSaverEmployer = new(0.035m, true, false, null, false) } }, "missing_verified_esct_rate");
        AssertIssue(Employee() with { Employee = Employee().Employee! with { HolidayPay = null } }, "missing_holiday_configuration");
    }

    [Theory]
    [InlineData(2026, 3, 31, false)]
    [InlineData(2026, 4, 1, true)]
    [InlineData(2027, 3, 31, true)]
    [InlineData(2027, 4, 1, false)]
    public void TaxYear_IsSelectedByActualPayDate(int year, int month, int day, bool supported)
    {
        Assert.Equal(supported, PayrollSettlementCalculator.Calculate(Employee() with { PayDate = new(year, month, day) }).IsSuccess);
    }

    [Fact]
    public void TemporaryKiwiSaverReduction_RequiresApproval_AndSuspensionRequiresExplicitZero()
    {
        var request = Employee() with { Employee = Employee().Employee! with { KiwiSaverEmployee = new(0.03m, true, false) } };
        AssertIssue(request, "invalid_kiwisaver_rate");
        request = request with { Employee = request.Employee! with { KiwiSaverEmployee = new(0.03m, true, true), KiwiSaverEmployer = new(0.03m, true, true, 0.175m, true) } };
        Assert.Equal(30m, PayrollSettlementCalculator.Calculate(request).Calculation!.EmployeeKiwiSaver);
        request = request with { Employee = request.Employee! with { KiwiSaverEmployee = new(0, false, false), KiwiSaverEmployer = new(0, false, false, null, false) } };
        Assert.Equal(0, PayrollSettlementCalculator.Calculate(request).Calculation!.EmployeeKiwiSaver);
    }

    private static SettlementRequest Contractor() => new(new(2026, 9, 9), SettlementPayFrequency.Weekly,
        100m, SettlementWorkerType.Contractor, null, new(0.20m, true, false, 0.15m, true));

    [Theory]
    [InlineData("kiwiSaverEmployee", "contributionsRequired", "missing_kiwisaver_configuration")]
    [InlineData("kiwiSaverEmployer", "contributionsRequired", "missing_employer_configuration")]
    public void MissingJsonContributionDecision_CannotBecomeZeroContribution(string configuration, string property, string issue)
    {
        var request = Employee() with { Employee = Employee().Employee! with
        {
            KiwiSaverEmployee = new(0m, false, false),
            KiwiSaverEmployer = new(0m, false, false, null, false)
        } };
        var json = JsonSerializer.SerializeToNode(request, JsonSerializerOptions.Web)!;
        json["employee"]![configuration]!.AsObject().Remove(property);
        var deserialised = JsonSerializer.Deserialize<SettlementRequest>(json.ToJsonString(), JsonSerializerOptions.Web)!;
        AssertIssue(deserialised, issue);
        Assert.True(PayrollSettlementCalculator.Calculate(request).IsSuccess);
    }

    [Theory]
    [InlineData("kiwiSaverEmployee", "temporaryReductionApproved", "invalid_kiwisaver_rate")]
    [InlineData("kiwiSaverEmployer", "temporaryReductionApproved", "missing_employer_reduction_declaration")]
    [InlineData("kiwiSaverEmployer", "esctRateVerified", "missing_verified_esct_rate")]
    [InlineData("holidayPay", "eligibilityConfirmed", "holiday_pay_ineligible")]
    [InlineData("holidayPay", "writtenAgreementConfirmed", "holiday_pay_ineligible")]
    public void MissingJsonEmployeeApproval_BlocksCalculation(string configuration, string property, string issue)
    {
        var request = Employee() with { Employee = Employee().Employee! with
        {
            KiwiSaverEmployee = new(0.03m, true, true),
            KiwiSaverEmployer = new(0.03m, true, true, 0.175m, true),
            HolidayPay = new(HolidayPayMode.PayAsYouGoEightPercent, true, true, HolidayPayEligibility.GenuineFixedTermUnderTwelveMonths)
        } };
        var json = JsonSerializer.SerializeToNode(request, JsonSerializerOptions.Web)!;
        json["employee"]![configuration]!.AsObject().Remove(property);
        AssertIssue(JsonSerializer.Deserialize<SettlementRequest>(json.ToJsonString(), JsonSerializerOptions.Web)!, issue);
    }

    [Theory]
    [InlineData("withholdingVerified", "missing_verified_withholding")]
    [InlineData("exemptionVerified", "missing_verified_withholding")]
    [InlineData("gstTreatmentVerified", "missing_verified_gst")]
    public void MissingJsonContractorDeclaration_BlocksCalculation(string property, string issue)
    {
        var request = Contractor() with { Contractor = new(0m, false, true, 0m, true) };
        var json = JsonSerializer.SerializeToNode(request, JsonSerializerOptions.Web)!;
        json["contractor"]!.AsObject().Remove(property);
        AssertIssue(JsonSerializer.Deserialize<SettlementRequest>(json.ToJsonString(), JsonSerializerOptions.Web)!, issue);
    }

    private static void AssertIssue(SettlementRequest request, string code)
    {
        var result = PayrollSettlementCalculator.Calculate(request);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Calculation);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }
}
