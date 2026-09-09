using System.Text.Json;
using FluentAssertions;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Services.Payroll;

namespace Nimpression.Application.Tests.Payroll;

public sealed class DriverPayslipDtoTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    public void DriverResponse_ReportsUnknownDeductionsWithoutInventingNetPayOrRate(int recordedRate)
    {
        var source = new PayslipDto(
            Id: Guid.NewGuid(), PayPeriodId: Guid.NewGuid(),
            PeriodStartsOn: new DateOnly(2026, 9, 1), PeriodEndsOn: new DateOnly(2026, 9, 14),
            DriverId: Guid.NewGuid(), DriverName: null, EmployeeNo: null,
            OrdinaryHours: 80m, OvertimeHours: 4m, HolidayHours: 2m,
            HourlyRateSnapshot: recordedRate, HoursBasedGross: 2800m,
            CompletedTripCount: 0, TotalDistanceKm: 0m, PerTripRateSnapshot: 0m,
            PerKmRateSnapshot: 0m, TripBasedGross: 0m, BasisUsed: PayBasis.Hourly,
            GrossPay: 2800m, Currency: "NZD", MinimumWageTopUp: false,
            CalculatedAt: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero),
            FinalisedAt: new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero),
            PaidAt: null, Lines: [], ShiftDetails: [], TripDetails: [], Fines: [],
            FinesLegalNotice: PayslipDto.DefaultFinesLegalNotice);

        var calculation = new SettlementCalculation("2026/27", 1000m, 0m, 1000m,
            100m, 17m, 117m, 20m, 35m, 35m, 6.13m, 28.87m, 0m, 0m, 828m, 1035m);
        var request = new SettlementRequest(new DateOnly(2026, 9, 15), SettlementPayFrequency.Weekly,
            1000m, SettlementWorkerType.Employee, null, null);
        var snapshot = new PayslipSettlementSnapshot(request, calculation, "rules-test",
            new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));
        var settled = DriverPayslipDto.FromPayslip(source with { Settlement = snapshot });
        settled.GrossPay.Should().Be(1000m);
        settled.Deductions.Should().Be(172m, "ACC is already included in PAYE and ESCT is an employer tax");
        settled.NetPay.Should().Be(828m);
        settled.DeductionCalculationStatus.Should().Be("Calculated");
        settled.Settlement.Should().Be(snapshot);
        var contractor = DriverPayslipDto.FromPayslip(source with { Settlement = snapshot with {
            Calculation = calculation with { Paye = 0m, StudentLoan = 0m, EmployeeKiwiSaver = 0m,
                Gst = 150m, ContractorWithholding = 200m, NetPay = 950m }
        } });
        contractor.GrossPay.Should().Be(1000m, "GST is shown separately from gross earnings");
        contractor.Deductions.Should().Be(200m);
        contractor.NetPay.Should().Be(950m);

        var response = DriverPayslipDto.FromPayslip(source);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response,
            JsonSerializerOptions.Web));
        var body = json.RootElement;

        body.GetProperty("grossPay").GetDecimal().Should().Be(2800m);
        body.GetProperty("netPay").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("deductions").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("deductionCalculationStatus").GetString().Should().Be("NotCalculated");
        body.GetProperty("hourlyRate").GetDecimal().Should().Be(recordedRate);
        body.GetProperty("totalHours").GetDecimal().Should().Be(86m);
        body.GetProperty("periodStartsOn").GetString().Should().Be("2026-09-01");
        body.GetProperty("periodEndsOn").GetString().Should().Be("2026-09-14");
        body.TryGetProperty("payPeriod", out _).Should().BeFalse();
        source.FinalisedAt.Should().Be(new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero));
    }
}
