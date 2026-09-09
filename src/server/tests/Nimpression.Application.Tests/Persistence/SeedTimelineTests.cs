using FluentAssertions;
using Nimpression.Domain.Enums;
using Nimpression.Infrastructure.Persistence.Seed;

namespace Nimpression.Application.Tests.Persistence;

public sealed class SeedTimelineTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(23)]
    public void HistoricalLifecycleEvents_DoNotExceedInjectedAsOf(int hour)
    {
        var asOf = new DateTimeOffset(2026, 8, 23, hour, 0, 0, TimeSpan.FromHours(12));
        var (users, drivers) = UserDriverSeeder.Generate();
        var (vehicles, _, _) = VehicleSeeder.Generate(drivers, users);
        var (areas, _) = AreaSeeder.Generate(drivers);
        var tasks = DispatchSeeder.Generate(areas, drivers, vehicles, users, asOf: asOf);
        var shifts = TimesheetSeeder.Generate(drivers, vehicles, users, asOf: asOf);
        var (periods, payslips) = PayrollSeeder.Generate(drivers, asOf: asOf);

        tasks.Where(task => task.CompletedAt.HasValue).Should().NotBeEmpty();
        tasks.SelectMany(task => task.DomainEvents).Should().OnlyContain(domainEvent => domainEvent.OccurredAt <= asOf);
        tasks.SelectMany(task => new[] { task.AcknowledgedAt, task.StartedAt, task.CompletedAt, task.CancelledAt })
            .Where(at => at.HasValue).Should().OnlyContain(at => at!.Value <= asOf);
        shifts.Should().NotBeEmpty().And.OnlyContain(shift => shift.ClockInAt <= asOf);
        shifts.SelectMany(shift => new[] { shift.ClockOutAt, shift.CorrectedAt })
            .Where(at => at.HasValue).Should().OnlyContain(at => at!.Value <= asOf);
        periods.SelectMany(period => new[] { period.FinalisedAt, period.PaidAt })
            .Where(at => at.HasValue).Should().OnlyContain(at => at!.Value <= asOf);
        payslips.Should().OnlyContain(slip => slip.CalculatedAt <= asOf);
        payslips.Where(slip => slip.FinalisedAt.HasValue).Should().OnlyContain(slip => slip.FinalisedAt!.Value <= asOf);
        foreach (var slip in payslips)
        {
            slip.FinalisedAt.Should().Be(periods.Single(period => period.Id == slip.PayPeriodId).FinalisedAt);
        }
        periods.Where(period => period.Status == PayPeriodStatus.Paid).Should().NotBeEmpty();
        if (hour < 12)
        {
            tasks.Should().Contain(task => task.ScheduledFor > asOf && task.Status == JobTaskStatus.Draft);
        }
    }

    [Fact]
    public void DefaultSeedAnchor_IsDeterministicAndBeforeTheReviewedNzDate()
    {
        var reviewedAsOf = new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.FromHours(12));
        SeedConstants.ReferenceNow.Should().BeBefore(reviewedAsOf);
        var (users, drivers) = UserDriverSeeder.Generate();
        var (vehicles, _, _) = VehicleSeeder.Generate(drivers, users);
        var shifts = TimesheetSeeder.Generate(drivers, vehicles, users);
        shifts.Should().OnlyContain(shift => shift.ClockInAt <= SeedConstants.ReferenceNow);
        var (periods, _) = PayrollSeeder.Generate(drivers);
        periods.Where(period => period.FinalisedAt.HasValue)
            .Should().OnlyContain(period => period.FinalisedAt!.Value <= SeedConstants.ReferenceNow);
    }
}
