using FluentAssertions;
using Nimpression.Application.Features.Payroll.Commands.CalculatePayslipSettlement;
using Nimpression.Application.Features.Payroll.Commands.FinalisePayPeriod;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Application.Tests.Payroll.TestDoubles;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.Services.Payroll;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Tests.Payroll.Commands;

public sealed class CalculatePayslipSettlementCommandHandlerTests
{
    private readonly FakePayrollRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _currentUser = new(role: UserRole.Admin);
    private readonly FakeAuditSink _auditSink = new();
    private readonly FakeDateTimeProvider _clock = new(new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));

    internal static SettlementRequest EmployeeSettings(decimal gross = 0) => new(
        new DateOnly(2026, 9, 4), SettlementPayFrequency.Fortnightly, gross, SettlementWorkerType.Employee,
        new EmployeeSettlementProfile("M", new(0m, false, false), new(0m, false, false, null, false),
            new(HolidayPayMode.OrdinaryAccrual, false, false)), null);

    private Payslip Seed(decimal gross = 240m, decimal operational = 240m, string currency = "NZD")
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods.Add(period.Id, period);
        var slip = new Payslip(Guid.NewGuid(), period.Id, Guid.NewGuid(), new WorkHours(8m), WorkHours.Zero, WorkHours.Zero,
            new Money(30m), new Money(operational), 0, Kilometres.Zero, Money.Zero(), Money.Zero(), Money.Zero(),
            PayBasis.Hourly, new Money(gross, currency), gross > operational, _clock.UtcNow);
        _repository.Payslips.Add(slip.Id, slip);
        return slip;
    }

    private CalculatePayslipSettlementCommandHandler Handler() => new(_repository, _unitOfWork, _currentUser, _auditSink, _clock);

    [Fact]
    public async Task ReplacesClientGrossAndProjectsSavedCalculationWithoutFineDeductions()
    {
        var slip = Seed();
        var result = await Handler().Handle(new(slip.Id, EmployeeSettings(999999m)), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Settlement!.Request.GrossEarnings.Should().Be(240m);
        result.Value.Settlement.Calculation.BaseGross.Should().Be(240m);
        // M fortnightly: $240 x (10.5% income tax + 1.75% ACC) = $29.40 total PAYE.
        result.Value.NetPay.Should().Be(210.60m);
        result.Value.Deductions.Should().Be(29.40m);
        result.Value.SettlementStatus.Should().Be("Calculated");
        slip.Settlement!.CalculatedAt.Should().Be(_clock.UtcNow);
        slip.Settlement.RulesVersion.Should().Be(PayrollSettlementCalculator.RulesVersion);
    }

    [Fact]
    public async Task ContractorGrossExcludesEmployeeMinimumWageTopUp()
    {
        var slip = Seed(gross: 240m, operational: 200m);
        var settings = new SettlementRequest(new DateOnly(2026, 9, 4), SettlementPayFrequency.Fortnightly,
            999999m, SettlementWorkerType.Contractor, null, new(0.2m, true, false, 0.15m, true));
        var result = await Handler().Handle(new(slip.Id, settings), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Settlement!.Calculation.BaseGross.Should().Be(200m);
        result.Value.NetPay.Should().Be(190m); // 200 + 30 GST - 40 withholding
    }

    [Fact]
    public async Task DriverCannotCalculateAnotherDriversSettlement()
    {
        var slip = Seed();
        _currentUser.Role = UserRole.Driver;
        var result = await Handler().Handle(new(slip.Id, EmployeeSettings()), CancellationToken.None);
        result.Error!.Code.Should().Be("forbidden");
        slip.Settlement.Should().BeNull();
    }

    [Fact]
    public async Task MissingProfileReturnsStableBusinessIssueWithoutSaving()
    {
        var slip = Seed();
        var result = await Handler().Handle(new(slip.Id, EmployeeSettings() with { Employee = null }), CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        slip.Settlement.Should().BeNull();
    }

    [Fact]
    public async Task FinalisationRequiresSettlementAndFreezesTheSnapshot()
    {
        var slip = Seed();
        var finaliser = new FinalisePayPeriodCommandHandler(_repository, _unitOfWork, _currentUser, _auditSink, _clock);
        var missing = await finaliser.Handle(new(slip.PayPeriodId), CancellationToken.None);
        missing.Error!.Code.Should().Be("payroll_settlement_required");
        slip.FinalisedAt.Should().BeNull();
        (await Handler().Handle(new(slip.Id, EmployeeSettings()), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await finaliser.Handle(new(slip.PayPeriodId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var snapshot = slip.Settlement!;
        (await Handler().Handle(new(slip.Id, EmployeeSettings()), CancellationToken.None)).Error!.Code.Should().Be("payslip_finalised");
        var change = () => slip.SetSettlement(snapshot with { CalculatedAt = _clock.UtcNow.AddDays(1) });
        change.Should().Throw<DomainValidationException>();
        slip.Settlement.Should().BeSameAs(snapshot);
    }

    [Fact]
    public async Task RejectsNonNzdEarningsWithoutSavingSettlement()
    {
        var slip = Seed(currency: "AUD");
        var result = await Handler().Handle(new(slip.Id, EmployeeSettings()), CancellationToken.None);
        result.Error!.Code.Should().Be("payroll_currency_unsupported");
        slip.Settlement.Should().BeNull();
    }

    [Fact]
    public async Task RejectsPayDateBeforeThePeriodEnds()
    {
        var slip = Seed();
        var result = await Handler().Handle(new(slip.Id,
            EmployeeSettings() with { PayDate = new DateOnly(2026, 8, 29) }), CancellationToken.None);
        result.Error!.Code.Should().Be("payroll_pay_date_invalid");
        slip.Settlement.Should().BeNull();
    }

    [Theory]
    [InlineData(SettlementPayFrequency.Weekly)]
    [InlineData(SettlementPayFrequency.FourWeekly)]
    [InlineData(SettlementPayFrequency.Monthly)]
    public async Task RejectsFrequencyThatDoesNotMatchTheFortnight(SettlementPayFrequency frequency)
    {
        var slip = Seed();
        var result = await Handler().Handle(new(slip.Id, EmployeeSettings() with { Frequency = frequency }), CancellationToken.None);
        result.Error!.Code.Should().Be("unsupported_pay_frequency");
        slip.Settlement.Should().BeNull();
    }

    [Theory]
    [InlineData(7, SettlementPayFrequency.Weekly)]
    [InlineData(28, SettlementPayFrequency.FourWeekly)]
    [InlineData(31, SettlementPayFrequency.Monthly)]
    public async Task AcceptsFrequencyMatchingACompletePeriod(int endsOnDay, SettlementPayFrequency frequency)
    {
        var slip = Seed();
        _repository.PayPeriods[slip.PayPeriodId] = new PayPeriod(slip.PayPeriodId,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, endsOnDay));
        var result = await Handler().Handle(new(slip.Id, EmployeeSettings() with { Frequency = frequency }), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value.Settlement!.Request.Frequency.Should().Be(frequency);
    }

    [Fact]
    public async Task StaleEmployeeGrossMustBeRecalculatedBeforeSettlement()
    {
        var slip = Seed(gross: 185.20m, operational: 176m); // Legacy $23.15 x 8 hours; current period needs $23.95 x 8.
        var result = await Handler().Handle(new(slip.Id, EmployeeSettings()), CancellationToken.None);
        result.Error!.Code.Should().Be("payroll_gross_recalculation_required");
        slip.GrossPay.Amount.Should().Be(185.20m);
        slip.Settlement.Should().BeNull();
    }

    [Fact]
    public void LegacyFinalisedPayslipRemainsReadableWithUnknownNet()
    {
        var slip = Seed();
        slip.Finalise(_clock.UtcNow);
        var dto = PayslipDto.FromEntity(slip, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        dto.GrossPay.Should().Be(240m);
        dto.NetPay.Should().BeNull();
        dto.Deductions.Should().BeNull();
        dto.SettlementStatus.Should().Be("NotCalculated");
    }
}
