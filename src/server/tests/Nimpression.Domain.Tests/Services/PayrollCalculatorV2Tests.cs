using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Domain.Tests.Services;

public class PayrollCalculatorV2Tests
{
    private static readonly DateOnly PeriodStart = new(2026, 8, 17); // Monday
    private static readonly DateOnly PeriodEnd = new(2026, 8, 30);   // Sunday (14 days)
    private static readonly PayPeriod TestPayPeriod = new(Guid.NewGuid(), PeriodStart, PeriodEnd);

    private static Driver CreateTestDriver(
        decimal hourlyRate = 30.00m,
        decimal perTripRate = 45.00m,
        decimal perKmRate = 1.20m)
    {
        return new Driver(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            employeeNo: "DRV-TEST01",
            licenceClass: "Class 2",
            licenceExpiry: new DateOnly(2028, 1, 1),
            hourlyRate: new Money(hourlyRate),
            perTripRate: new Money(perTripRate),
            perKmRate: new Money(perKmRate),
            phoneEnc: "enc_phone",
            addressEnc: "enc_address",
            emergencyContactEnc: "enc_contact",
            hiredOn: new DateOnly(2025, 1, 1));
    }

    private static ShiftEntry CreateCompletedShift(
        Guid driverId,
        DateTimeOffset clockInAt,
        double durationHours,
        int breakMinutes = 0)
    {
        var shift = new ShiftEntry(Guid.NewGuid(), driverId, clockInAt);
        shift.ClockOut(clockInAt.AddHours(durationHours), breakMinutes: breakMinutes);
        return shift;
    }

    private static JobTask CreateCompletedTask(
        Guid driverId,
        DateTimeOffset completedAt,
        decimal? plannedKm = null,
        decimal? actualKm = null,
        decimal? startOdo = null,
        decimal? endOdo = null)
    {
        var task = new JobTask(
            id: Guid.NewGuid(),
            @ref: "TASK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            title: "Delivery Task",
            areaId: Guid.NewGuid(),
            scheduledFor: completedAt.AddHours(-2),
            createdByUserId: Guid.NewGuid(),
            plannedDistanceKm: plannedKm.HasValue ? new Kilometres(plannedKm.Value) : null,
            driverId: driverId,
            vehicleId: Guid.NewGuid());

        task.Acknowledge(completedAt.AddHours(-1.5));
        task.Start(completedAt.AddHours(-1), startOdo.HasValue ? new Kilometres(startOdo.Value) : null);
        task.Complete(
            completedAt: completedAt,
            actualDistanceKm: actualKm.HasValue ? new Kilometres(actualKm.Value) : null,
            endOdometerKm: endOdo.HasValue ? new Kilometres(endOdo.Value) : null);

        return task;
    }

    [Fact]
    public void F7_1_ThreeRateTiers_CalculatesOrdinaryOvertimeAndHolidayHoursCorrectly()
    {
        // 1.0x (<=8h regular day), 1.5x (>8h regular day), 2.0x (public holiday day)
        var driver = CreateTestDriver(hourlyRate: 30.00m, perTripRate: 0m, perKmRate: 0m);
        var nzOffset = TimeSpan.FromHours(12);

        // Day 1 (Regular): 10h total -> 8h Ordinary (1.0x) + 2h Overtime (1.5x)
        var day1ClockIn = new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset);
        var shift1 = CreateCompletedShift(driver.Id, day1ClockIn, durationHours: 10, breakMinutes: 0);

        // Day 2 (Public Holiday): 6h total -> 6h Holiday (2.0x)
        var holidayDate = new DateOnly(2026, 8, 18);
        var day2ClockIn = new DateTimeOffset(2026, 8, 18, 8, 0, 0, nzOffset);
        var shift2 = CreateCompletedShift(driver.Id, day2ClockIn, durationHours: 6, breakMinutes: 0);

        var publicHolidays = new HashSet<DateOnly> { holidayDate };

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift1, shift2],
            tasks: [],
            publicHolidays: publicHolidays);

        Assert.Equal(8.00m, payslip.OrdinaryHours.Value);
        Assert.Equal(2.00m, payslip.OvertimeHours.Value);
        Assert.Equal(6.00m, payslip.HolidayHours.Value);

        // Ordinary: 8h * $30 = $240
        // Overtime: 2h * ($30 * 1.5 = $45) = $90
        // Holiday:  6h * ($30 * 2.0 = $60) = $360
        // Total HoursBasedGross = $240 + $90 + $360 = $690.00
        Assert.Equal(690.00m, payslip.HoursBasedGross.Amount);
        Assert.Equal(690.00m, payslip.GrossPay.Amount);
        Assert.Equal(PayBasis.Hourly, payslip.BasisUsed);
    }

    [Fact]
    public void F7_2_OnlyCompletedTasksCount_CancelledAndNonCompletedTasksIgnored()
    {
        var driver = CreateTestDriver(hourlyRate: 20.00m, perTripRate: 50.00m, perKmRate: 1.00m);
        var nzOffset = TimeSpan.FromHours(12);
        var completedTime = new DateTimeOffset(2026, 8, 20, 14, 0, 0, nzOffset);

        // 1 Completed Task (50km) -> $50 base + 50 * $1 = $100
        var completedTask = CreateCompletedTask(driver.Id, completedTime, plannedKm: 50m);

        // 1 Cancelled Task
        var cancelledTask = new JobTask(
            id: Guid.NewGuid(),
            @ref: "TASK-CANCELLED",
            title: "Cancelled Task",
            areaId: Guid.NewGuid(),
            scheduledFor: completedTime,
            createdByUserId: Guid.NewGuid(),
            plannedDistanceKm: new Kilometres(100m),
            driverId: driver.Id,
            vehicleId: Guid.NewGuid());
        cancelledTask.Cancel("Customer cancelled", completedTime);

        // 1 In-progress Task
        var inProgressTask = new JobTask(
            id: Guid.NewGuid(),
            @ref: "TASK-INPROG",
            title: "In Progress Task",
            areaId: Guid.NewGuid(),
            scheduledFor: completedTime,
            createdByUserId: Guid.NewGuid(),
            plannedDistanceKm: new Kilometres(80m),
            driverId: driver.Id,
            vehicleId: Guid.NewGuid());
        inProgressTask.Acknowledge(completedTime.AddHours(-1));
        inProgressTask.Start(completedTime);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [],
            tasks: [completedTask, cancelledTask, inProgressTask]);

        Assert.Equal(1, payslip.CompletedTripCount);
        Assert.Equal(50.00m, payslip.TotalDistanceKm.Value);
        Assert.Equal(100.00m, payslip.TripBasedGross.Amount);
        Assert.Equal(100.00m, payslip.GrossPay.Amount);
        Assert.Equal(PayBasis.Trip, payslip.BasisUsed);
    }

    [Fact]
    public void F7_3_MaxGrossSelection_Case1_HoursWins()
    {
        var driver = CreateTestDriver(hourlyRate: 40.00m, perTripRate: 30.00m, perKmRate: 0.50m);
        var nzOffset = TimeSpan.FromHours(12);

        // Hours: 8h * $40 = $320.00
        var shift = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), 8);

        // Trip: 2 trips * $30 + 50km * $0.50 = $60 + $25 = $85.00
        var task1 = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset), plannedKm: 25m);
        var task2 = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 16, 0, 0, nzOffset), plannedKm: 25m);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift],
            tasks: [task1, task2]);

        Assert.Equal(320.00m, payslip.HoursBasedGross.Amount);
        Assert.Equal(85.00m, payslip.TripBasedGross.Amount);
        Assert.Equal(320.00m, payslip.GrossPay.Amount);
        Assert.Equal(PayBasis.Hourly, payslip.BasisUsed);
    }

    [Fact]
    public void F7_3_MaxGrossSelection_Case2_TripWins()
    {
        var driver = CreateTestDriver(hourlyRate: 25.00m, perTripRate: 80.00m, perKmRate: 1.50m);
        var nzOffset = TimeSpan.FromHours(12);

        // Hours: 4h * $25 = $100.00
        var shift = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), 4);

        // Trip: 3 trips * $80 + 200km * $1.50 = $240 + $300 = $540.00
        var task1 = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 10, 0, 0, nzOffset), plannedKm: 100m);
        var task2 = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 11, 0, 0, nzOffset), plannedKm: 50m);
        var task3 = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset), plannedKm: 50m);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift],
            tasks: [task1, task2, task3]);

        Assert.Equal(100.00m, payslip.HoursBasedGross.Amount);
        Assert.Equal(540.00m, payslip.TripBasedGross.Amount);
        Assert.Equal(540.00m, payslip.GrossPay.Amount);
        Assert.Equal(PayBasis.Trip, payslip.BasisUsed);
    }

    [Fact]
    public void F7_3_MaxGrossSelection_Case3_EqualRecordsHourly()
    {
        var driver = CreateTestDriver(hourlyRate: 25.00m, perTripRate: 50.00m, perKmRate: 1.00m);
        var nzOffset = TimeSpan.FromHours(12);

        // Hours: 8h * $25 = $200.00
        var shift = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), 8);

        // Trip: 2 trips * $50 + 100km * $1.00 = $100 + $100 = $200.00
        var task1 = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset), plannedKm: 50m);
        var task2 = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 16, 0, 0, nzOffset), plannedKm: 50m);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift],
            tasks: [task1, task2]);

        Assert.Equal(200.00m, payslip.HoursBasedGross.Amount);
        Assert.Equal(200.00m, payslip.TripBasedGross.Amount);
        Assert.Equal(200.00m, payslip.GrossPay.Amount);
        Assert.Equal(PayBasis.Hourly, payslip.BasisUsed); // Equal must be Hourly
    }

    [Fact]
    public void F7_4_DualBasisDetailsRetained_UnselectedBasisStillFullyProduced()
    {
        var driver = CreateTestDriver(hourlyRate: 30.00m, perTripRate: 50.00m, perKmRate: 1.00m);
        var nzOffset = TimeSpan.FromHours(12);

        // Hours: 8h * $30 = $240.00 (Hourly wins)
        var shift = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), 8);

        // Trip: 1 trip * $50 + 30km * $1.00 = $80.00 (Unselected)
        var task = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset), plannedKm: 30m);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift],
            tasks: [task]);

        Assert.Equal(PayBasis.Hourly, payslip.BasisUsed);

        // All lines from both Hourly and Trip must be present
        Assert.Contains(payslip.Lines, l => l.Basis == PayBasis.Hourly && l.Kind == "OrdinaryHours" && l.Amount.Amount == 240.00m);
        Assert.Contains(payslip.Lines, l => l.Basis == PayBasis.Hourly && l.Kind == "OvertimeHours" && l.Amount.Amount == 0.00m);
        Assert.Contains(payslip.Lines, l => l.Basis == PayBasis.Hourly && l.Kind == "HolidayHours" && l.Amount.Amount == 0.00m);
        Assert.Contains(payslip.Lines, l => l.Basis == PayBasis.Trip && l.Kind == "TripBase" && l.Amount.Amount == 50.00m && l.Qty == 1);
        Assert.Contains(payslip.Lines, l => l.Basis == PayBasis.Trip && l.Kind == "Mileage" && l.Amount.Amount == 30.00m && l.Distance!.Value.Value == 30.00m);
    }

    [Fact]
    public void F7_5_MinimumWageFloor_Case1_TripWinsOperationalGross_ButConvertedHourlyBelowMinimum()
    {
        // 40 hours total. Minimum wage = $23.15/h -> Floor = 40 * $23.15 = $926.00
        // Driver hourly rate = $20.00/h -> HoursBasedGross = 40 * $20 = $800.00
        // Trip: 10 trips * $50 + 350km * $1.00 = $500 + $350 = $850.00
        // Operational gross = MAX(800, 850) = $850 (Trip wins)
        // Converted hourly = $850 / 40h = $21.25/h < $23.15/h minimum wage
        // Top-up needed = $926.00 - $850.00 = $76.00
        // Final GrossPay = $926.00
        var driver = CreateTestDriver(hourlyRate: 20.00m, perTripRate: 50.00m, perKmRate: 1.00m);
        var nzOffset = TimeSpan.FromHours(12);

        var shifts = new List<ShiftEntry>();
        for (var day = 17; day <= 21; day++) // 5 days * 8h = 40h
        {
            shifts.Add(CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, day, 8, 0, 0, nzOffset), 8));
        }

        var tasks = new List<JobTask>();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17 + (i % 5), 14, 0, 0, nzOffset), plannedKm: 35m));
        }

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: shifts,
            tasks: tasks,
            minimumHourlyWage: new Money(23.15m));

        Assert.Equal(800.00m, payslip.HoursBasedGross.Amount);
        Assert.Equal(850.00m, payslip.TripBasedGross.Amount);
        Assert.Equal(PayBasis.Trip, payslip.BasisUsed);
        Assert.True(payslip.MinimumWageTopUp);
        Assert.Equal(926.00m, payslip.GrossPay.Amount);

        var topUpLine = payslip.Lines.FirstOrDefault(l => l.Kind == "MinimumWageTopUp");
        Assert.NotNull(topUpLine);
        Assert.Equal(76.00m, topUpLine.Amount.Amount);
        Assert.Equal(PayBasis.Trip, topUpLine.Basis);
    }

    [Fact]
    public void F7_5_MinimumWageFloor_Case2_DriverHourlyRateItselfBelowMinimumWage()
    {
        // Driver hourly rate $18.00 < $23.15. 40h worked -> HoursBasedGross = $720.00
        // Trip gross = $0
        // Floor = 40 * $23.15 = $926.00
        // Top-up = $926.00 - $720.00 = $206.00
        // Final GrossPay = $926.00
        var driver = CreateTestDriver(hourlyRate: 18.00m, perTripRate: 0m, perKmRate: 0m);
        var nzOffset = TimeSpan.FromHours(12);

        var shifts = new List<ShiftEntry>();
        for (var day = 17; day <= 21; day++)
        {
            shifts.Add(CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, day, 8, 0, 0, nzOffset), 8));
        }

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: shifts,
            tasks: [],
            minimumHourlyWage: new Money(23.15m));

        Assert.Equal(720.00m, payslip.HoursBasedGross.Amount);
        Assert.Equal(0.00m, payslip.TripBasedGross.Amount);
        Assert.Equal(PayBasis.Hourly, payslip.BasisUsed);
        Assert.True(payslip.MinimumWageTopUp);
        Assert.Equal(926.00m, payslip.GrossPay.Amount);

        var topUpLine = payslip.Lines.FirstOrDefault(l => l.Kind == "MinimumWageTopUp");
        Assert.NotNull(topUpLine);
        Assert.Equal(206.00m, topUpLine.Amount.Amount);
    }

    [Fact]
    public void F7_5_MinimumWageFloor_Case3_AfterTopUp_GrossPayExactlyEquals_TotalHoursMultipliedByMinimumWage()
    {
        // Random work hours across multiple days with overtime
        var driver = CreateTestDriver(hourlyRate: 15.00m, perTripRate: 10.00m, perKmRate: 0.10m);
        var nzOffset = TimeSpan.FromHours(12);

        // Day 1: 10h (8h ord @ $15 = $120, 2h ot @ $22.50 = $45) = $165
        // Day 2: 7.5h (7.5h ord @ $15 = $112.50)
        // Total hours = 17.5h. Total hours gross = $277.50
        // Minimum wage floor = 17.5 * $23.15 = $405.125 -> Math.Round to $405.13 (Money VO)
        var shift1 = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), 10);
        var shift2 = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 18, 8, 0, 0, nzOffset), 7.5);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift1, shift2],
            tasks: [],
            minimumHourlyWage: new Money(23.15m));

        var totalHours = payslip.OrdinaryHours + payslip.OvertimeHours + payslip.HolidayHours;
        Assert.Equal(17.50m, totalHours.Value);

        var expectedFloor = new Money(totalHours.Value * 23.15m);
        Assert.Equal(expectedFloor, payslip.GrossPay);
        Assert.True(payslip.MinimumWageTopUp);
    }

    [Fact]
    public void F7_6_MileageSourcePriority_AndOdometerValidation()
    {
        var driver = CreateTestDriver(hourlyRate: 30.00m, perTripRate: 10.00m, perKmRate: 1.00m);
        var nzOffset = TimeSpan.FromHours(12);
        var completedTime = new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset);

        // Case A: End and Start Odometer present -> takes End - Start = 120km (ignores Planned=50, Actual=80)
        var taskA = CreateCompletedTask(driver.Id, completedTime, plannedKm: 50m, actualKm: 80m, startOdo: 1000m, endOdo: 1120m);
        Assert.Equal(120m, taskA.EffectiveDistanceKm!.Value.Value);

        // Case B: Only ActualDistance present -> takes Actual = 75km (ignores Planned=50)
        var taskB = CreateCompletedTask(driver.Id, completedTime.AddHours(1), plannedKm: 50m, actualKm: 75m);
        Assert.Equal(75m, taskB.EffectiveDistanceKm!.Value.Value);

        // Case C: Only PlannedDistance present -> takes Planned = 45km
        var taskC = CreateCompletedTask(driver.Id, completedTime.AddHours(2), plannedKm: 45m);
        Assert.Equal(45m, taskC.EffectiveDistanceKm!.Value.Value);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [],
            tasks: [taskA, taskB, taskC]);

        Assert.Equal(3, payslip.CompletedTripCount);
        Assert.Equal(120m + 75m + 45m, payslip.TotalDistanceKm.Value); // 240km
        Assert.Equal(3 * 10.00m + 240 * 1.00m, payslip.TripBasedGross.Amount); // $270.00
    }

    [Fact]
    public void F7_6_OdometerDifference_NegativeOrExceeding1000Km_ThrowsDomainValidationException()
    {
        var driver = CreateTestDriver();
        var nzOffset = TimeSpan.FromHours(12);
        var completedTime = new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset);

        // Negative diff in JobTask Complete directly throws DomainValidationException
        Assert.Throws<DomainValidationException>(() =>
            CreateCompletedTask(driver.Id, completedTime, startOdo: 1500m, endOdo: 1400m));

        // Diff > 1000km in JobTask Complete directly throws DomainValidationException
        Assert.Throws<DomainValidationException>(() =>
            CreateCompletedTask(driver.Id, completedTime, startOdo: 1000m, endOdo: 2005m));
    }

    [Fact]
    public void F7_7_CrossPeriodAttribution_ShiftsBelongToClockInDay_TasksBelongToCompletedDay()
    {
        var driver = CreateTestDriver(hourlyRate: 30.00m, perTripRate: 50.00m, perKmRate: 1.00m);
        var nzOffset = TimeSpan.FromHours(12);

        // Shift 1: Clock-in on 2026-08-30 22:00 (inside period), clock-out on 2026-08-31 06:00 (outside period)
        // AttributedDate is 2026-08-30 -> SHOULD be counted in TestPayPeriod!
        var crossShiftInPeriod = CreateCompletedShift(
            driver.Id,
            new DateTimeOffset(2026, 8, 30, 22, 0, 0, nzOffset),
            durationHours: 8);

        // Shift 2: Clock-in on 2026-08-16 22:00 (before period), clock-out on 2026-08-17 06:00 (inside period)
        // AttributedDate is 2026-08-16 -> SHOULD NOT be counted in TestPayPeriod!
        var crossShiftBeforePeriod = CreateCompletedShift(
            driver.Id,
            new DateTimeOffset(2026, 8, 16, 22, 0, 0, nzOffset),
            durationHours: 8);

        // Task 1: Scheduled on 2026-08-30 20:00 (inside period), completed on 2026-08-31 01:00 (outside period)
        // CompletedAt date is 2026-08-31 -> SHOULD NOT be counted in TestPayPeriod!
        var taskCompletedNextPeriod = CreateCompletedTask(
            driver.Id,
            new DateTimeOffset(2026, 8, 31, 1, 0, 0, nzOffset),
            plannedKm: 50m);

        // Task 2: Scheduled on 2026-08-16 20:00 (before period), completed on 2026-08-17 02:00 (inside period)
        // CompletedAt date is 2026-08-17 -> SHOULD be counted in TestPayPeriod!
        var taskCompletedInPeriod = CreateCompletedTask(
            driver.Id,
            new DateTimeOffset(2026, 8, 17, 2, 0, 0, nzOffset),
            plannedKm: 60m);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [crossShiftInPeriod, crossShiftBeforePeriod],
            tasks: [taskCompletedNextPeriod, taskCompletedInPeriod]);

        // Only crossShiftInPeriod counted (8h ordinary = $240)
        Assert.Equal(8.00m, payslip.OrdinaryHours.Value);
        Assert.Equal(240.00m, payslip.HoursBasedGross.Amount);

        // Only taskCompletedInPeriod counted (1 trip * $50 + 60km * $1 = $110)
        Assert.Equal(1, payslip.CompletedTripCount);
        Assert.Equal(60.00m, payslip.TotalDistanceKm.Value);
        Assert.Equal(110.00m, payslip.TripBasedGross.Amount);
    }

    [Fact]
    public void F7_12_FinesDoNotAffectPayrollGrossPay()
    {
        // NZ Wages Protection Act 1983: Fine has ZERO computational relationship with Payslip
        var driver = CreateTestDriver(hourlyRate: 30.00m, perTripRate: 50.00m, perKmRate: 1.00m);
        var nzOffset = TimeSpan.FromHours(12);

        var shift = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), 8);
        var task = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset), plannedKm: 40m);

        // Fine issued for driver during pay period
        var fine = new Fine(
            id: Guid.NewGuid(),
            driverId: driver.Id,
            vehicleId: Guid.NewGuid(),
            issuedOn: new DateOnly(2026, 8, 18),
            authority: "NZ Police",
            reference: "INF-888999",
            amount: new Money(150.00m),
            reason: "Speeding");

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift],
            tasks: [task]);

        // Gross pay should strictly be based on formula, untouched by $150 fine
        Assert.Equal(240.00m, payslip.HoursBasedGross.Amount); // 8h * $30
        Assert.Equal(90.00m, payslip.TripBasedGross.Amount);   // $50 + 40km * $1
        Assert.Equal(240.00m, payslip.GrossPay.Amount);
        Assert.DoesNotContain(payslip.Lines, l => l.Kind == "Fine" || l.Description.Contains("Fine"));
    }

    [Fact]
    public void F7_5_MinimumWageFloor_Boundary_OperationalGrossEqualsFloor_NoTopUp()
    {
        // 边界测试：实收金额恰好等于最低工资地板线时，MinimumWageTopUp 必须为 false 且不产出 MinimumWageTopUp 明细行
        // 司机时薪 $23.15，趟次和公里费率设 0，工作 1 小时 -> 实际工时薪资 $23.15，最低工资地板 $23.15
        var driver = CreateTestDriver(hourlyRate: 23.15m, perTripRate: 0m, perKmRate: 0m);
        var nzOffset = TimeSpan.FromHours(12);
        var shift = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), durationHours: 1);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift],
            tasks: [],
            minimumHourlyWage: new Money(23.15m));

        Assert.Equal(23.15m, payslip.HoursBasedGross.Amount);
        Assert.Equal(23.15m, payslip.GrossPay.Amount);
        Assert.False(payslip.MinimumWageTopUp);
        Assert.Equal(PayBasis.Hourly, payslip.BasisUsed);
        Assert.DoesNotContain(payslip.Lines, l => l.Kind == "MinimumWageTopUp");
    }

    [Fact]
    public void F7_5_MinimumWageFloor_Boundary_TripGrossEqualsFloor_NoTopUp()
    {
        // 边界测试：趟次胜且金额恰好等于最低工资地板线时，不触发补差
        // 司机时薪 $10/h，1 小时班次（工时口径 $10）；趟次费率 $23.15/trip，1 趟任务（趟次口径 $23.15）。
        // 最低工资地板 = 1h * $23.15 = $23.15。
        // 实收恰好等于地板线：BasisUsed 为 Trip，MinimumWageTopUp 为 false，Lines 不含 MinimumWageTopUp
        var driver = CreateTestDriver(hourlyRate: 10.00m, perTripRate: 23.15m, perKmRate: 0m);
        var nzOffset = TimeSpan.FromHours(12);
        var shift = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), durationHours: 1);
        var task = CreateCompletedTask(driver.Id, new DateTimeOffset(2026, 8, 17, 10, 0, 0, nzOffset));

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift],
            tasks: [task],
            minimumHourlyWage: new Money(23.15m));

        Assert.Equal(10.00m, payslip.HoursBasedGross.Amount);
        Assert.Equal(23.15m, payslip.TripBasedGross.Amount);
        Assert.Equal(23.15m, payslip.GrossPay.Amount);
        Assert.False(payslip.MinimumWageTopUp);
        Assert.Equal(PayBasis.Trip, payslip.BasisUsed);
        Assert.DoesNotContain(payslip.Lines, l => l.Kind == "MinimumWageTopUp");
    }

    [Fact]
    public void F7_6_OdometerDifference_Boundary_Exactly1000Km_IsAllowed()
    {
        // 边界测试：里程差恰好等于 1000km (2000 - 1000 = 1000km) 为合法上限，不抛异常
        var driver = CreateTestDriver(hourlyRate: 30.00m, perTripRate: 50.00m, perKmRate: 1.00m);
        var nzOffset = TimeSpan.FromHours(12);
        var completedTime = new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset);

        var taskExactly1000 = CreateCompletedTask(driver.Id, completedTime, startOdo: 1000m, endOdo: 2000m);
        Assert.Equal(1000m, taskExactly1000.EffectiveDistanceKm!.Value.Value);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [],
            tasks: [taskExactly1000]);

        Assert.Equal(1, payslip.CompletedTripCount);
        Assert.Equal(1000m, payslip.TotalDistanceKm.Value);
        Assert.Equal(50.00m + 1000m * 1.00m, payslip.TripBasedGross.Amount);
    }

    [Fact]
    public void F7_1_DailyWorkHours_Boundary_Exactly8Hours_NoOvertime()
    {
        // 边界测试：当日工时恰好 8.0 小时 -> 8.0h 常规（1.0x），0.0h 加班（1.5x）
        var driver = CreateTestDriver(hourlyRate: 30.00m, perTripRate: 0m, perKmRate: 0m);
        var nzOffset = TimeSpan.FromHours(12);
        var shift = CreateCompletedShift(driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset), durationHours: 8);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: TestPayPeriod,
            shifts: [shift],
            tasks: []);

        Assert.Equal(8.00m, payslip.OrdinaryHours.Value);
        Assert.Equal(0.00m, payslip.OvertimeHours.Value);
        Assert.Equal(0.00m, payslip.HolidayHours.Value);
        Assert.Equal(240.00m, payslip.HoursBasedGross.Amount);
    }
}
