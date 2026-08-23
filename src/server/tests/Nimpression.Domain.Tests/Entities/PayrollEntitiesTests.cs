using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class PayrollEntitiesTests
{
    [Fact]
    public void PayPeriod_lifecycle_and_guards()
    {
        var start = new DateOnly(2026, 8, 1);
        var end = new DateOnly(2026, 8, 14);
        var period = new PayPeriod(Guid.NewGuid(), start, end);

        Assert.Equal(PayPeriodStatus.Open, period.Status);
        Assert.True(period.Contains(new DateOnly(2026, 8, 5)));
        Assert.False(period.Contains(new DateOnly(2026, 8, 15)));

        period.SetStatus(PayPeriodStatus.Calculating);
        Assert.Equal(PayPeriodStatus.Calculating, period.Status);

        var finalTime = DateTimeOffset.UtcNow;
        period.Finalise(finalTime);
        Assert.Equal(PayPeriodStatus.Finalised, period.Status);
        Assert.Equal(finalTime, period.FinalisedAt);

        var paidTime = DateTimeOffset.UtcNow;
        period.MarkPaid(paidTime);
        Assert.Equal(PayPeriodStatus.Paid, period.Status);
        Assert.Equal(paidTime, period.PaidAt);

        // Cannot mark paid if not finalised
        var newPeriod = new PayPeriod(Guid.NewGuid(), start, end);
        Assert.Throws<DomainValidationException>(() => newPeriod.MarkPaid(DateTimeOffset.UtcNow));

        // Start after End
        Assert.Throws<DomainValidationException>(() => new PayPeriod(Guid.NewGuid(), end, start));
    }

    [Fact]
    public void Payslip_initialization_and_finalisation_with_event()
    {
        var id = Guid.NewGuid();
        var periodId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var payslip = new Payslip(
            id,
            periodId,
            driverId,
            new WorkHours(70m),
            new WorkHours(10m),
            new WorkHours(8m),
            new Money(30m),
            new Money(2940m),
            15,
            new Kilometres(500m),
            new Money(40m),
            new Money(0.60m),
            new Money(900m),
            PayBasis.Hourly,
            new Money(2940m),
            false,
            now);

        Assert.Equal(id, payslip.Id);
        Assert.Equal(periodId, payslip.PayPeriodId);
        Assert.Equal(driverId, payslip.DriverId);
        Assert.Equal(new WorkHours(70m), payslip.OrdinaryHours);
        Assert.Equal(new WorkHours(10m), payslip.OvertimeHours);
        Assert.Equal(new WorkHours(8m), payslip.HolidayHours);
        Assert.Equal(new Money(30m), payslip.HourlyRateSnapshot);
        Assert.Equal(new Money(2940m), payslip.HoursBasedGross);
        Assert.Equal(15, payslip.CompletedTripCount);
        Assert.Equal(new Kilometres(500m), payslip.TotalDistanceKm);
        Assert.Equal(PayBasis.Hourly, payslip.BasisUsed);
        Assert.Equal(new Money(2940m), payslip.GrossPay);
        Assert.False(payslip.MinimumWageTopUp);

        var line = new PayslipLine(
            Guid.NewGuid(),
            id,
            PayBasis.Hourly,
            "OrdinaryHours",
            "70 hours @ $30/h",
            new Money(30m),
            new Money(2100m),
            hours: new WorkHours(70m));

        payslip.AddLine(line);
        Assert.Single(payslip.Lines);

        var finalTime = now.AddHours(1);
        payslip.Finalise(finalTime);
        Assert.Equal(finalTime, payslip.FinalisedAt);

        var domainEvent = Assert.IsType<PayslipFinalised>(Assert.Single(payslip.DomainEvents));
        Assert.Equal(id, domainEvent.PayslipId);
        Assert.Equal(periodId, domainEvent.PayPeriodId);
        Assert.Equal(driverId, domainEvent.DriverId);
        Assert.Equal(new Money(2940m), domainEvent.GrossPay);

        // Cannot add lines after finalisation
        Assert.Throws<DomainValidationException>(() => payslip.AddLine(line));
        // Cannot finalise twice
        Assert.Throws<DomainValidationException>(() => payslip.Finalise(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Payslip_validation_guards()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<DomainValidationException>(() => new Payslip(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(),
            WorkHours.Zero, WorkHours.Zero, WorkHours.Zero,
            Money.Zero(), Money.Zero(), 0, Kilometres.Zero,
            Money.Zero(), Money.Zero(), Money.Zero(), PayBasis.Hourly, Money.Zero(), false, now));

        Assert.Throws<DomainValidationException>(() => new Payslip(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty,
            WorkHours.Zero, WorkHours.Zero, WorkHours.Zero,
            Money.Zero(), Money.Zero(), 0, Kilometres.Zero,
            Money.Zero(), Money.Zero(), Money.Zero(), PayBasis.Hourly, Money.Zero(), false, now));

        Assert.Throws<DomainValidationException>(() => new Payslip(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WorkHours.Zero, WorkHours.Zero, WorkHours.Zero,
            Money.Zero(), Money.Zero(), -1, Kilometres.Zero,
            Money.Zero(), Money.Zero(), Money.Zero(), PayBasis.Hourly, Money.Zero(), false, now));
    }
}
