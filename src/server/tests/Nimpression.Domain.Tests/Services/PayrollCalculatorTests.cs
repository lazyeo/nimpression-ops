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

        // 16 hours total = 16 * 35 = $560
        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 3, 8, 0, 0, NzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 3, 16, 0, 0, NzOffset)); // 8h
        var shift2 = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 4, 8, 0, 0, NzOffset));
        shift2.ClockOut(new DateTimeOffset(2026, 8, 4, 16, 0, 0, NzOffset)); // 8h

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

    [Fact]
    public void Payroll_minimum_wage_top_up_fallbacks_to_hourly()
    {
        // Hourly rate: $15/h (low test rate), Trip rate: $50, Km rate: $0
        var driver = CreateDriver(hourlyRate: 15m, tripRate: 50m, kmRate: 0m);
        var period = CreatePayPeriod();

        // 40 hours worked in period
        // HoursBasedGross = 40 * $15 = $600
        var shifts = new List<ShiftEntry>();
        for (var d = 3; d <= 7; d++) // 5 days * 8h = 40h
        {
            var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, d, 8, 0, 0, NzOffset));
            shift.ClockOut(new DateTimeOffset(2026, 8, d, 16, 0, 0, NzOffset));
            shifts.Add(shift);
        }

        // 14 completed tasks @ $50 = $700
        // Initial Trip gross ($700) > Hours gross ($600)
        // BUT: $700 / 40h = $17.50/h < NZ minimum wage ($23.15/h)
        var tasks = new List<JobTask>();
        for (var i = 0; i < 14; i++)
        {
            var task = new JobTask(Guid.NewGuid(), $"T-{i}", "Task", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
            task.Acknowledge(DateTimeOffset.UtcNow);
            task.Start(DateTimeOffset.UtcNow);
            task.Complete(new DateTimeOffset(2026, 8, 3, 9, 0, 0, NzOffset), actualDistanceKm: Kilometres.Zero);
            tasks.Add(task);
        }

        var result = PayrollCalculator.Calculate(driver, period, shifts, tasks);

        Assert.Equal(new Money(600m), result.HoursBasedGross);
        Assert.Equal(new Money(700m), result.TripBasedGross);
        // Force fallback to Hourly and flag MinimumWageTopUp
        Assert.Equal(PayBasis.Hourly, result.BasisUsed);
        Assert.Equal(new Money(600m), result.GrossPay);
        Assert.True(result.MinimumWageTopUp);
    }

    [Fact]
    public void Payroll_only_counts_completed_tasks()
    {
        var driver = CreateDriver(hourlyRate: 20m, tripRate: 50m, kmRate: 1.0m);
        var period = CreatePayPeriod();

        // Task 1: Completed
        var completedTask = new JobTask(Guid.NewGuid(), "T-COMP", "Completed", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
        completedTask.Acknowledge(DateTimeOffset.UtcNow);
        completedTask.Start(DateTimeOffset.UtcNow);
        completedTask.Complete(new DateTimeOffset(2026, 8, 5, 10, 0, 0, NzOffset), actualDistanceKm: new Kilometres(30m));

        // Task 2: Draft
        var draftTask = new JobTask(Guid.NewGuid(), "T-DRAFT", "Draft", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid());

        // Task 3: Assigned
        var assignedTask = new JobTask(Guid.NewGuid(), "T-ASGN", "Assigned", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());

        // Task 4: InProgress
        var progressTask = new JobTask(Guid.NewGuid(), "T-PROG", "Progress", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
        progressTask.Acknowledge(DateTimeOffset.UtcNow);
        progressTask.Start(DateTimeOffset.UtcNow);

        // Task 5: Cancelled
        var cancelledTask = new JobTask(Guid.NewGuid(), "T-CANC", "Cancelled", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
        cancelledTask.Cancel("Customer changed mind", DateTimeOffset.UtcNow);

        var allTasks = new[] { completedTask, draftTask, assignedTask, progressTask, cancelledTask };

        var result = PayrollCalculator.Calculate(driver, period, shifts: [], tasks: allTasks);

        Assert.Equal(1, result.CompletedTripCount);
        Assert.Equal(new Kilometres(30m), result.TotalDistanceKm);
        // 1 * 50 + 30 * 1.0 = $80
        Assert.Equal(new Money(80m), result.TripBasedGross);
    }

    [Fact]
    public void Payroll_snapshot_freezing_and_payslip_creation()
    {
        var driver = CreateDriver(hourlyRate: 30m, tripRate: 40m, kmRate: 0.50m);
        var period = CreatePayPeriod();

        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 3, 8, 0, 0, NzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 3, 16, 0, 0, NzOffset)); // 8h @ 30 = $240

        var calcResult = PayrollCalculator.Calculate(driver, period, [shift], []);
        var payslipId = Guid.NewGuid();
        var payslip = calcResult.CreatePayslip(payslipId, period.Id, driver.Id);

        Assert.Equal(payslipId, payslip.Id);
        Assert.Equal(new Money(30m), payslip.HourlyRateSnapshot);
        Assert.Equal(new Money(240m), payslip.GrossPay);

        // Modify driver rates after calculation
        driver.UpdateRates(new Money(50m), new Money(100m), new Money(2.00m));

        // Payslip snapshot MUST remain unchanged
        Assert.Equal(new Money(30m), payslip.HourlyRateSnapshot);
        Assert.Equal(new Money(240m), payslip.GrossPay);
    }

    [Fact]
    public void Payroll_filters_out_shifts_and_tasks_outside_pay_period()
    {
        var driver = CreateDriver(hourlyRate: 30m, tripRate: 50m);
        // PayPeriod: 2026-08-03 to 2026-08-16
        var period = CreatePayPeriod(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 16));

        // Shift 1: Inside (2026-08-05) -> 8h
        var shiftInside = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 5, 8, 0, 0, NzOffset));
        shiftInside.ClockOut(new DateTimeOffset(2026, 8, 5, 16, 0, 0, NzOffset));

        // Shift 2: Before period (2026-08-02) -> 8h
        var shiftBefore = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 2, 8, 0, 0, NzOffset));
        shiftBefore.ClockOut(new DateTimeOffset(2026, 8, 2, 16, 0, 0, NzOffset));

        // Shift 3: After period (2026-08-17) -> 8h
        var shiftAfter = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, NzOffset));
        shiftAfter.ClockOut(new DateTimeOffset(2026, 8, 17, 16, 0, 0, NzOffset));

        // Task 1: Completed Inside
        var taskInside = new JobTask(Guid.NewGuid(), "T-IN", "In", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
        taskInside.Acknowledge(DateTimeOffset.UtcNow);
        taskInside.Start(DateTimeOffset.UtcNow);
        taskInside.Complete(new DateTimeOffset(2026, 8, 10, 10, 0, 0, NzOffset), actualDistanceKm: new Kilometres(10m));

        // Task 2: Completed After
        var taskAfter = new JobTask(Guid.NewGuid(), "T-OUT", "Out", Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), driverId: driver.Id, vehicleId: Guid.NewGuid());
        taskAfter.Acknowledge(DateTimeOffset.UtcNow);
        taskAfter.Start(DateTimeOffset.UtcNow);
        taskAfter.Complete(new DateTimeOffset(2026, 8, 18, 10, 0, 0, NzOffset), actualDistanceKm: new Kilometres(10m));

        var result = PayrollCalculator.Calculate(
            driver,
            period,
            [shiftInside, shiftBefore, shiftAfter],
            [taskInside, taskAfter]);

        // Only shiftInside counted: 8h
        Assert.Equal(new WorkHours(8.00m), result.OrdinaryHours);
        Assert.Equal(new Money(240m), result.HoursBasedGross);

        // Only taskInside counted: 1 task
        Assert.Equal(1, result.CompletedTripCount);
        Assert.Equal(new Kilometres(10m), result.TotalDistanceKm);
    }
}
