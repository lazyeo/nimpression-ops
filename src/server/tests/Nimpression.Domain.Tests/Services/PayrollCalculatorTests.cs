using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Services;

public sealed class PayrollCalculatorTests
{
    private static readonly TimeSpan NzOffset = TimeSpan.FromHours(12);

    private static Driver CreateDriver(
        decimal hourlyRate = 30m,
        decimal tripRate = 40m,
        decimal kmRate = 0.60m)
    {
        return new Driver(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "EMP001",
            "Class 4",
            new DateOnly(2028, 12, 31),
            new Money(hourlyRate),
            new Money(tripRate),
            new Money(kmRate),
            "phone",
            "address",
            "emergency",
            new DateOnly(2025, 1, 1));
    }

    private static PayPeriod CreatePayPeriod(
        DateOnly? startsOn = null,
        DateOnly? endsOn = null)
    {
        var start = startsOn ?? new DateOnly(2026, 8, 3); // Monday
        var end = endsOn ?? new DateOnly(2026, 8, 16);     // Sunday (2-week fortnight)
        return new PayPeriod(Guid.NewGuid(), start, end);
    }

    [Fact]
    public void Payroll_hours_three_rate_tiers_ordinary_overtime_and_holiday()
    {
        // Rate: $30/h
        var driver = CreateDriver(hourlyRate: 30m);
        var period = CreatePayPeriod();

        // Day 1 (2026-08-03): 10 hours work (8h ordinary 1.0x, 2h overtime 1.5x)
        var shift1 = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 3, 7, 0, 0, NzOffset));
        shift1.ClockOut(new DateTimeOffset(2026, 8, 3, 17, 0, 0, NzOffset)); // 10h

        // Day 2 (2026-08-04): NZ Public Holiday, 8 hours work (8h holiday 2.0x)
        var holidayDate = new DateOnly(2026, 8, 4);
        var shift2 = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 4, 8, 0, 0, NzOffset));
        shift2.ClockOut(new DateTimeOffset(2026, 8, 4, 16, 0, 0, NzOffset)); // 8h

        var publicHolidays = new HashSet<DateOnly> { holidayDate };

        var result = PayrollCalculator.Calculate(
            driver,
            period,
            shifts: [shift1, shift2],
            tasks: [],
            publicHolidays: publicHolidays);

        // Ordinary: 8h * $30 = $240
        // Overtime: 2h * ($30 * 1.5 = $45) = $90
        // Holiday: 8h * ($30 * 2.0 = $60) = $480
        // Total HoursBasedGross = 240 + 90 + 480 = $810
        Assert.Equal(new WorkHours(8.00m), result.OrdinaryHours);
        Assert.Equal(new WorkHours(2.00m), result.OvertimeHours);
        Assert.Equal(new WorkHours(8.00m), result.HolidayHours);
        Assert.Equal(new Money(810.00m), result.HoursBasedGross);
        Assert.Equal(PayBasis.Hourly, result.BasisUsed);
        Assert.Equal(new Money(810.00m), result.GrossPay);
        Assert.False(result.MinimumWageTopUp);
        Assert.Equal(3, result.Lines.Count);
    }

    [Fact]
    public void Payroll_basis_competition_hourly_wins()
    {
        var driver = CreateDriver(hourlyRate: 35m, tripRate: 20m, kmRate: 0.20m);
        var period = CreatePayPeriod();

        // 40 hours worked = $1400
        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 3, 8, 0, 0, NzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 3, 16, 0, 0, NzOffset)); // 8h
        var shift2 = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 4, 8, 0, 0, NzOffset));
        shift2.ClockOut(new DateTimeOffset(2026, 8, 4, 16, 0, 0, NzOffset)); // 8h
        // 16h total = 16 * 35 = $560

        // 2 completed tasks: 2 * $20 + (50km * $0.20) = $40 + $10 = $50
        var task1 = new JobTask(Guid.NewGuid(), "T-1", "Job 1", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), plannedDistanceKm: new Kilometres(25m), driverId: driver.Id, vehicleId: Guid.NewGuid());
        task1.Acknowledge(DateTimeOffset.UtcNow);
        task1.Start(DateTimeOffset.UtcNow);
        task1.Complete(new DateTimeOffset(2026, 8, 3, 12, 0, 0, NzOffset), actualDistanceKm: new Kilometres(25m));

        var task2 = new JobTask(Guid.NewGuid(), "T-2", "Job 2", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), plannedDistanceKm: new Kilometres(25m), driverId: driver.Id, vehicleId: Guid.NewGuid());
        task2.Acknowledge(DateTimeOffset.UtcNow);
        task2.Start(DateTimeOffset.UtcNow);
        task2.Complete(new DateTimeOffset(2026, 8, 4, 12, 0, 0, NzOffset), actualDistanceKm: new Kilometres(25m));

        var result = PayrollCalculator.Calculate(driver, period, [shift, shift2], [task1, task2]);

        Assert.Equal(new Money(560m), result.HoursBasedGross);
        Assert.Equal(new Money(50m), result.TripBasedGross);
        Assert.Equal(PayBasis.Hourly, result.BasisUsed);
        Assert.Equal(new Money(560m), result.GrossPay);
        Assert.False(result.MinimumWageTopUp);
    }

    [Fact]
    public void Payroll_basis_competition_trip_wins()
    {
        var driver = CreateDriver(hourlyRate: 25m, tripRate: 100m, kmRate: 1.00m);
        var period = CreatePayPeriod();

        // 8 hours worked = 8 * 25 = $200
        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 3, 8, 0, 0, NzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 3, 16, 0, 0, NzOffset));

        // 5 tasks: 5 * 100 + 200km * 1.0 = 500 + 200 = $700
        // Effective hourly rate: $700 / 8h = $87.50/h >= $23.15/h -> Trip wins cleanly!
        var tasks = new List<JobTask>();
        for (var i = 0; i < 5; i++)
        {
            var task = new JobTask(Guid.NewGuid(), $"T-{i}", "Task", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
            task.Acknowledge(DateTimeOffset.UtcNow);
            task.Start(DateTimeOffset.UtcNow);
            task.Complete(new DateTimeOffset(2026, 8, 3, 10 + i, 0, 0, NzOffset), actualDistanceKm: new Kilometres(40m));
            tasks.Add(task);
        }

        var result = PayrollCalculator.Calculate(driver, period, [shift], tasks);

        Assert.Equal(new Money(200m), result.HoursBasedGross);
        Assert.Equal(new Money(700m), result.TripBasedGross);
        Assert.Equal(PayBasis.Trip, result.BasisUsed);
        Assert.Equal(new Money(700m), result.GrossPay);
        Assert.False(result.MinimumWageTopUp);
    }

    [Fact]
    public void Payroll_basis_competition_exact_tie_favours_hourly()
    {
        var driver = CreateDriver(hourlyRate: 25m, tripRate: 100m, kmRate: 0m);
        var period = CreatePayPeriod();

        // 8 hours @ $25 = $200
        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 5, 8, 0, 0, NzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 5, 16, 0, 0, NzOffset));

        // 2 tasks @ $100 = $200
        var task1 = new JobTask(Guid.NewGuid(), "T-1", "Task 1", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
        task1.Acknowledge(DateTimeOffset.UtcNow);
        task1.Start(DateTimeOffset.UtcNow);
        task1.Complete(new DateTimeOffset(2026, 8, 5, 10, 0, 0, NzOffset), actualDistanceKm: Kilometres.Zero);

        var task2 = new JobTask(Guid.NewGuid(), "T-2", "Task 2", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
        task2.Acknowledge(DateTimeOffset.UtcNow);
        task2.Start(DateTimeOffset.UtcNow);
        task2.Complete(new DateTimeOffset(2026, 8, 5, 14, 0, 0, NzOffset), actualDistanceKm: Kilometres.Zero);

        var result = PayrollCalculator.Calculate(driver, period, [shift], [task1, task2]);

        Assert.Equal(new Money(200m), result.HoursBasedGross);
        Assert.Equal(new Money(200m), result.TripBasedGross);
        Assert.Equal(PayBasis.Hourly, result.BasisUsed); // Exact tie MUST use Hourly
        Assert.Equal(new Money(200m), result.GrossPay);
        Assert.False(result.MinimumWageTopUp);
    }
}
