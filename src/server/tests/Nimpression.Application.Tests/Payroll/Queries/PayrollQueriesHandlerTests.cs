using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Application.Features.Payroll.Queries.GetDriverPayslips;
using Nimpression.Application.Features.Payroll.Queries.GetPayPeriodById;
using Nimpression.Application.Features.Payroll.Queries.GetPayPeriodPayslips;
using Nimpression.Application.Features.Payroll.Queries.GetPayPeriodsList;
using Nimpression.Application.Features.Payroll.Queries.GetPayslipById;
using Nimpression.Application.Tests.Payroll.TestDoubles;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Payroll.Queries;

public sealed class PayrollQueriesHandlerTests
{
    private readonly FakePayrollRepository _repository = new();

    private static Driver CreateDriver(Guid userId, string empNo = "DRV-001")
    {
        return new Driver(
            id: Guid.NewGuid(),
            userId: userId,
            employeeNo: empNo,
            licenceClass: "Class 2",
            licenceExpiry: new DateOnly(2028, 1, 1),
            hourlyRate: new Money(30m),
            perTripRate: new Money(40m),
            perKmRate: new Money(1m),
            phoneEnc: "phone",
            addressEnc: "addr",
            emergencyContactEnc: "emrg",
            hiredOn: new DateOnly(2025, 1, 1));
    }

    [Fact]
    public async Task F7_10_DriverQueryingOtherDriverPayslip_Returns403Forbidden()
    {
        var driver1User = Guid.NewGuid();
        var driver2User = Guid.NewGuid();

        var driver1 = CreateDriver(driver1User, "DRV-001");
        var driver2 = CreateDriver(driver2User, "DRV-002");
        _repository.Drivers[driver1.Id] = driver1;
        _repository.Drivers[driver2.Id] = driver2;

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        // Finalised payslip for Driver 2
        var payslip2 = PayrollCalculatorV2.Calculate(
            driver: driver2,
            payPeriod: period,
            shifts: [],
            tasks: []);
        payslip2.Finalise(DateTimeOffset.UtcNow);
        _repository.Payslips[payslip2.Id] = payslip2;

        // Logged in as Driver 1
        var driver1CurrentUser = new FakeCurrentUser(userId: driver1User, role: UserRole.Driver);
        var handler = new GetPayslipByIdQueryHandler(_repository, driver1CurrentUser);

        // Driver 1 tries to query Driver 2's payslip
        var result = await handler.Handle(new GetPayslipByIdQuery(payslip2.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        // F7.10 明确要求：查他人 403（不是 404）
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task F7_10_DriverQueryingOwnDraftPayslip_Returns403Forbidden()
    {
        var driverUser = Guid.NewGuid();
        var driver = CreateDriver(driverUser, "DRV-001");
        _repository.Drivers[driver.Id] = driver;

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        // Draft (unfinalised) payslip for Driver 1
        var draftPayslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: period,
            shifts: [],
            tasks: []);
        _repository.Payslips[draftPayslip.Id] = draftPayslip;

        var driverCurrentUser = new FakeCurrentUser(userId: driverUser, role: UserRole.Driver);
        var handler = new GetPayslipByIdQueryHandler(_repository, driverCurrentUser);

        var result = await handler.Handle(new GetPayslipByIdQuery(draftPayslip.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal("payslip_not_finalised", result.Error.Code);
    }

    [Fact]
    public async Task F7_10_DriverQueryingOwnFinalisedPayslip_Success()
    {
        var driverUser = Guid.NewGuid();
        var driver = CreateDriver(driverUser, "DRV-001");
        _repository.Drivers[driver.Id] = driver;

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var finalisedPayslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: period,
            shifts: [],
            tasks: []);
        finalisedPayslip.Finalise(DateTimeOffset.UtcNow);
        _repository.Payslips[finalisedPayslip.Id] = finalisedPayslip;

        var driverCurrentUser = new FakeCurrentUser(userId: driverUser, role: UserRole.Driver);
        var handler = new GetPayslipByIdQueryHandler(_repository, driverCurrentUser);

        var result = await handler.Handle(new GetPayslipByIdQuery(finalisedPayslip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(finalisedPayslip.Id, result.Value.Id);
        Assert.Equal("DRV-001", result.Value.EmployeeNo);
    }

    [Fact]
    public async Task F7_11_And_F7_12_PayslipTraceability_And_FineLegalSeparation()
    {
        var adminUser = new FakeCurrentUser(role: UserRole.Admin);
        var driver = CreateDriver(Guid.NewGuid(), "DRV-001");
        _repository.Drivers[driver.Id] = driver;

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var nzOffset = TimeSpan.FromHours(12);

        // Shift
        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 17, 16, 0, 0, nzOffset), breakMinutes: 30);
        _repository.Shifts.Add(shift);

        // JobTask
        var completedAt = new DateTimeOffset(2026, 8, 17, 14, 0, 0, nzOffset);
        var task = new JobTask(
            id: Guid.NewGuid(),
            @ref: "TASK-001",
            title: "Traceable Task",
            areaId: Guid.NewGuid(),
            scheduledFor: completedAt.AddHours(-2),
            createdByUserId: Guid.NewGuid(),
            plannedDistanceKm: new Kilometres(30m),
            driverId: driver.Id,
            vehicleId: Guid.NewGuid());
        task.Acknowledge(completedAt.AddHours(-1.5));
        task.Start(completedAt.AddHours(-1));
        task.Complete(completedAt, actualDistanceKm: new Kilometres(32m));
        _repository.Tasks.Add(task);

        // Fine (F7.12)
        var fine = new Fine(
            id: Guid.NewGuid(),
            driverId: driver.Id,
            vehicleId: Guid.NewGuid(),
            issuedOn: new DateOnly(2026, 8, 18),
            authority: "NZ Police",
            reference: "INF-777888",
            amount: new Money(120m),
            reason: "Bus lane infringement");
        _repository.Fines.Add(fine);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: period,
            shifts: [shift],
            tasks: [task]);
        _repository.Payslips[payslip.Id] = payslip;

        var handler = new GetPayslipByIdQueryHandler(_repository, adminUser);
        var result = await handler.Handle(new GetPayslipByIdQuery(payslip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // F7.11 & W37: 工时与趟次明细读取结算快照 Lines
        Assert.NotEmpty(result.Value.Lines);
        Assert.Contains(result.Value.Lines, l => l.Basis == PayBasis.Hourly && l.Kind == "OrdinaryHours" && l.Hours == 7.50m);
        Assert.Contains(result.Value.Lines, l => l.Basis == PayBasis.Trip && l.Kind == "TripBase" && l.Qty == 1);
        Assert.Contains(result.Value.Lines, l => l.Basis == PayBasis.Trip && l.Kind == "Mileage" && l.Distance == 32.00m);

        // F7.12: 工资单金额与罚款无计算关联；UI/API 分区展示并附法规说明
        Assert.Single(result.Value.Fines);
        Assert.Equal("INF-777888", result.Value.Fines[0].Reference);
        Assert.Equal(120.00m, result.Value.Fines[0].Amount);
        Assert.Contains("Wages Protection Act 1983", result.Value.FinesLegalNotice);
    }

    [Fact]
    public async Task W37_AC2_GetPayslipById_AfterShiftsOrTasksModified_DetailsDoNotChange_SnapshotImmutability()
    {
        // Arrange: 初始排班 8h 与 1 趟 25km 任务，完成计薪并生成工资单
        var adminUser = new FakeCurrentUser(role: UserRole.Admin);
        var driver = CreateDriver(Guid.NewGuid(), "DRV-001");
        _repository.Drivers[driver.Id] = driver;

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var nzOffset = TimeSpan.FromHours(12);
        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 17, 16, 0, 0, nzOffset), breakMinutes: 0); // 8h
        _repository.Shifts.Add(shift);

        var task = new JobTask(
            id: Guid.NewGuid(),
            @ref: "TASK-SNAP-01",
            title: "Snapshot Task",
            areaId: Guid.NewGuid(),
            scheduledFor: new DateTimeOffset(2026, 8, 17, 10, 0, 0, nzOffset),
            createdByUserId: Guid.NewGuid(),
            plannedDistanceKm: new Kilometres(25m),
            driverId: driver.Id,
            vehicleId: Guid.NewGuid());
        task.Acknowledge(new DateTimeOffset(2026, 8, 17, 10, 15, 0, nzOffset));
        task.Start(new DateTimeOffset(2026, 8, 17, 10, 30, 0, nzOffset));
        task.Complete(new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset), actualDistanceKm: new Kilometres(25m));
        _repository.Tasks.Add(task);

        var payslip = PayrollCalculatorV2.Calculate(
            driver: driver,
            payPeriod: period,
            shifts: [shift],
            tasks: [task]);
        _repository.Payslips[payslip.Id] = payslip;

        // Act 1: 结算后数据库中的班次数据被修改（例如管理员在工时表修正为 4 小时，或新增额外班次）
        var modifiedShift = new ShiftEntry(shift.Id, driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset));
        modifiedShift.ClockOut(new DateTimeOffset(2026, 8, 17, 12, 0, 0, nzOffset), breakMinutes: 0); // 变为 4h
        _repository.Shifts.Clear();
        _repository.Shifts.Add(modifiedShift);

        // 新增其他班次与任务
        var extraShift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 18, 8, 0, 0, nzOffset));
        extraShift.ClockOut(new DateTimeOffset(2026, 8, 18, 18, 0, 0, nzOffset), breakMinutes: 0); // 10h
        _repository.Shifts.Add(extraShift);

        _repository.Tasks.Clear(); // 清空任务表

        // Act 2: 查询工资单详情
        var handler = new GetPayslipByIdQueryHandler(_repository, adminUser);
        var result = await handler.Handle(new GetPayslipByIdQuery(payslip.Id), CancellationToken.None);

        // Assert: 核心断言——工资单所有明细行与字段必须严格保持结算时的快照状态，不随数据库后改动而变化
        Assert.True(result.IsSuccess);
        Assert.Equal(8.00m, result.Value.OrdinaryHours);
        Assert.Equal(0.00m, result.Value.OvertimeHours);
        Assert.Equal(1, result.Value.CompletedTripCount);
        Assert.Equal(25.00m, result.Value.TotalDistanceKm);

        // 验证 Lines 快照行保持不变
        var ordLine = Assert.Single(result.Value.Lines, l => l.Kind == "OrdinaryHours");
        Assert.Equal(8.00m, ordLine.Hours);
        Assert.Equal(240.00m, ordLine.Amount);

        var tripLine = Assert.Single(result.Value.Lines, l => l.Kind == "TripBase");
        Assert.Equal(1, tripLine.Qty);
        Assert.Equal(40.00m, tripLine.Amount);

        var distLine = Assert.Single(result.Value.Lines, l => l.Kind == "Mileage");
        Assert.Equal(25.00m, distLine.Distance);
        Assert.Equal(25.00m, distLine.Amount);
    }

    [Fact]
    public async Task GetPayslipById_Populates_DriverName_And_PaidAt_When_Available()
    {
        var adminUser = new FakeCurrentUser(role: UserRole.Admin);
        var driver = CreateDriver(Guid.NewGuid(), "DRV-001");
        _repository.Drivers[driver.Id] = driver;
        _repository.DriverDisplayNames[driver.Id] = "Liam Smith";

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 7, 26), new DateOnly(2026, 8, 8));
        var paidAt = new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.FromHours(12));
        period.Finalise(paidAt.AddDays(-2));
        period.MarkPaid(paidAt);
        _repository.PayPeriods[period.Id] = period;

        var payslip = PayrollCalculatorV2.Calculate(driver, period, [], []);
        payslip.Finalise(paidAt.AddDays(-2));
        _repository.Payslips[payslip.Id] = payslip;

        var handler = new GetPayslipByIdQueryHandler(_repository, adminUser);
        var result = await handler.Handle(new GetPayslipByIdQuery(payslip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Liam Smith", result.Value.DriverName);
        Assert.Equal("DRV-001", result.Value.EmployeeNo);
        Assert.Equal(paidAt, result.Value.PaidAt);
    }

    [Fact]
    public async Task GetPayPeriodPayslips_Populates_DriverName_And_PaidAt_For_All_Drivers()
    {
        var adminUser = new FakeCurrentUser(role: UserRole.Admin);
        var driver1 = CreateDriver(Guid.NewGuid(), "DRV-001");
        var driver2 = CreateDriver(Guid.NewGuid(), "DRV-002");
        _repository.Drivers[driver1.Id] = driver1;
        _repository.Drivers[driver2.Id] = driver2;
        _repository.DriverDisplayNames[driver1.Id] = "Liam Smith";
        _repository.DriverDisplayNames[driver2.Id] = "Noah Williams";

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 9), new DateOnly(2026, 8, 22));
        _repository.PayPeriods[period.Id] = period;

        var payslip1 = PayrollCalculatorV2.Calculate(driver1, period, [], []);
        var payslip2 = PayrollCalculatorV2.Calculate(driver2, period, [], []);
        _repository.Payslips[payslip1.Id] = payslip1;
        _repository.Payslips[payslip2.Id] = payslip2;

        var handler = new GetPayPeriodPayslipsQueryHandler(_repository, adminUser);
        var result = await handler.Handle(new GetPayPeriodPayslipsQuery(period.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        var p1 = result.Value.First(p => p.DriverId == driver1.Id);
        var p2 = result.Value.First(p => p.DriverId == driver2.Id);

        Assert.Equal("Liam Smith", p1.DriverName);
        Assert.Equal("Noah Williams", p2.DriverName);
        Assert.Null(p1.PaidAt); // Unpaid period has null PaidAt
    }

    [Fact]
    public async Task GetPayslipById_Traceability_StrictlyExcludes_Out_Of_Period_Shifts_And_Tasks()
    {
        // Arrange (W28 R2 验收：追溯数据必须严格属于该工资周期，周期外班次/趟次不可泄漏)
        var adminUser = new FakeCurrentUser(role: UserRole.Admin);
        var driver = CreateDriver(Guid.NewGuid(), "DRV-001");
        _repository.Drivers[driver.Id] = driver;

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 9), new DateOnly(2026, 8, 22));
        _repository.PayPeriods[period.Id] = period;
        var nzOffset = TimeSpan.FromHours(12);

        // 周期内班次 (8/15)
        var inPeriodShift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 15, 8, 0, 0, nzOffset));
        inPeriodShift.ClockOut(new DateTimeOffset(2026, 8, 15, 17, 0, 0, nzOffset), breakMinutes: 30); // 8.5h
        _repository.Shifts.Add(inPeriodShift);

        // 周期外班次 (8/08 前一周期 & 8/23 23/08–07/09 后一周期)
        var prePeriodShift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 8, 8, 0, 0, nzOffset));
        prePeriodShift.ClockOut(new DateTimeOffset(2026, 8, 8, 17, 0, 0, nzOffset), breakMinutes: 30);
        _repository.Shifts.Add(prePeriodShift);

        var postPeriodShift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 23, 8, 0, 0, nzOffset));
        postPeriodShift.ClockOut(new DateTimeOffset(2026, 8, 23, 17, 0, 0, nzOffset), breakMinutes: 30);
        _repository.Shifts.Add(postPeriodShift);

        // 周期内任务 (8/15)
        var inPeriodTask = new JobTask(
            Guid.NewGuid(), "TSK-IN-01", "In Period Task", Guid.NewGuid(),
            new DateTimeOffset(2026, 8, 15, 8, 0, 0, nzOffset), Guid.NewGuid(),
            description: null, priority: TaskPriority.Medium,
            plannedDistanceKm: new Kilometres(20m), driverId: driver.Id, vehicleId: Guid.NewGuid());
        inPeriodTask.Acknowledge(new DateTimeOffset(2026, 8, 15, 8, 30, 0, nzOffset));
        inPeriodTask.Start(new DateTimeOffset(2026, 8, 15, 9, 0, 0, nzOffset));
        inPeriodTask.Complete(new DateTimeOffset(2026, 8, 15, 12, 0, 0, nzOffset), actualDistanceKm: new Kilometres(22m));
        _repository.Tasks.Add(inPeriodTask);

        // 周期外任务 (8/25)
        var postPeriodTask = new JobTask(
            Guid.NewGuid(), "TSK-OUT-01", "Out Period Task", Guid.NewGuid(),
            new DateTimeOffset(2026, 8, 25, 8, 0, 0, nzOffset), Guid.NewGuid(),
            description: null, priority: TaskPriority.Medium,
            plannedDistanceKm: new Kilometres(20m), driverId: driver.Id, vehicleId: Guid.NewGuid());
        postPeriodTask.Acknowledge(new DateTimeOffset(2026, 8, 25, 8, 30, 0, nzOffset));
        postPeriodTask.Start(new DateTimeOffset(2026, 8, 25, 9, 0, 0, nzOffset));
        postPeriodTask.Complete(new DateTimeOffset(2026, 8, 25, 12, 0, 0, nzOffset), actualDistanceKm: new Kilometres(22m));
        _repository.Tasks.Add(postPeriodTask);

        var payslip = PayrollCalculatorV2.Calculate(driver, period, [inPeriodShift], [inPeriodTask]);
        _repository.Payslips[payslip.Id] = payslip;

        var handler = new GetPayslipByIdQueryHandler(_repository, adminUser);
        var result = await handler.Handle(new GetPayslipByIdQuery(payslip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // 验证 Lines 快照只计入周期内的 1 条班次（8h ord + 0.5h ot）和 1 条任务（22km），周期外绝不混入
        var ordLine = Assert.Single(result.Value.Lines, l => l.Kind == "OrdinaryHours");
        Assert.Equal(8.00m, ordLine.Hours);

        var otLine = Assert.Single(result.Value.Lines, l => l.Kind == "OvertimeHours");
        Assert.Equal(0.50m, otLine.Hours);

        var tripLine = Assert.Single(result.Value.Lines, l => l.Kind == "TripBase");
        Assert.Equal(1, tripLine.Qty);

        var distLine = Assert.Single(result.Value.Lines, l => l.Kind == "Mileage");
        Assert.Equal(22.00m, distLine.Distance);
    }

    [Fact]
    public async Task Dispatcher_QueryingPayPeriodsList_Returns403Forbidden()
    {
        var dispatcherUser = new FakeCurrentUser(role: UserRole.Dispatcher);
        var handler = new GetPayPeriodsListQueryHandler(_repository, dispatcherUser);

        var filter = new PayPeriodFilter(null, null, null, 1, 20);
        var result = await handler.Handle(new GetPayPeriodsListQuery(filter), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Admin_QueryingPayPeriodsList_Success()
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var adminUser = new FakeCurrentUser(role: UserRole.Admin);
        var handler = new GetPayPeriodsListQueryHandler(_repository, adminUser);

        var filter = new PayPeriodFilter(null, null, null, 1, 20);
        var result = await handler.Handle(new GetPayPeriodsListQuery(filter), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task Dispatcher_QueryingPayPeriodById_Returns403Forbidden()
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var dispatcherUser = new FakeCurrentUser(role: UserRole.Dispatcher);
        var handler = new GetPayPeriodByIdQueryHandler(_repository, dispatcherUser);

        var result = await handler.Handle(new GetPayPeriodByIdQuery(period.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Admin_QueryingPayPeriodById_Success()
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var adminUser = new FakeCurrentUser(role: UserRole.Admin);
        var handler = new GetPayPeriodByIdQueryHandler(_repository, adminUser);

        var result = await handler.Handle(new GetPayPeriodByIdQuery(period.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(period.Id, result.Value.Id);
    }

    [Fact]
    public async Task Dispatcher_QueryingPayPeriodPayslips_Returns403Forbidden()
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var dispatcherUser = new FakeCurrentUser(role: UserRole.Dispatcher);
        var handler = new GetPayPeriodPayslipsQueryHandler(_repository, dispatcherUser);

        var result = await handler.Handle(new GetPayPeriodPayslipsQuery(period.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Dispatcher_QueryingDriverPayslips_Returns403Forbidden()
    {
        var driver = CreateDriver(Guid.NewGuid(), "DRV-001");
        _repository.Drivers[driver.Id] = driver;

        var dispatcherUser = new FakeCurrentUser(role: UserRole.Dispatcher);
        var handler = new GetDriverPayslipsQueryHandler(_repository, dispatcherUser);

        var result = await handler.Handle(new GetDriverPayslipsQuery(DriverId: driver.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Driver_QueryingOwnDriverPayslipsList_Success()
    {
        var driverUser = Guid.NewGuid();
        var driver = CreateDriver(driverUser, "DRV-001");
        _repository.Drivers[driver.Id] = driver;

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var payslip = PayrollCalculatorV2.Calculate(driver, period, [], []);
        payslip.Finalise(DateTimeOffset.UtcNow);
        _repository.Payslips[payslip.Id] = payslip;

        var driverCurrentUser = new FakeCurrentUser(userId: driverUser, role: UserRole.Driver);
        var handler = new GetDriverPayslipsQueryHandler(_repository, driverCurrentUser);

        // Driver querying without specifying DriverId (auto-resolved from token)
        var result = await handler.Handle(new GetDriverPayslipsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(payslip.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task Driver_QueryingOtherDriverPayslipsList_Returns403Forbidden()
    {
        var driver1User = Guid.NewGuid();
        var driver2User = Guid.NewGuid();
        var driver1 = CreateDriver(driver1User, "DRV-001");
        var driver2 = CreateDriver(driver2User, "DRV-002");
        _repository.Drivers[driver1.Id] = driver1;
        _repository.Drivers[driver2.Id] = driver2;

        var driver1CurrentUser = new FakeCurrentUser(userId: driver1User, role: UserRole.Driver);
        var handler = new GetDriverPayslipsQueryHandler(_repository, driver1CurrentUser);

        // Driver 1 attempts to pass Driver 2's ID
        var result = await handler.Handle(new GetDriverPayslipsQuery(DriverId: driver2.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Dispatcher_QueryingPayslipById_Returns403Forbidden()
    {
        var driver = CreateDriver(Guid.NewGuid(), "DRV-001");
        _repository.Drivers[driver.Id] = driver;

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var payslip = PayrollCalculatorV2.Calculate(driver, period, [], []);
        payslip.Finalise(DateTimeOffset.UtcNow);
        _repository.Payslips[payslip.Id] = payslip;

        var dispatcherUser = new FakeCurrentUser(role: UserRole.Dispatcher);
        var handler = new GetPayslipByIdQueryHandler(_repository, dispatcherUser);

        var result = await handler.Handle(new GetPayslipByIdQuery(payslip.Id), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Equal("forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Admin_QueryingPayslipById_Success()
    {
        var driver = CreateDriver(Guid.NewGuid(), "DRV-001");
        _repository.Drivers[driver.Id] = driver;
        _repository.DriverDisplayNames[driver.Id] = "Liam Smith";

        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var payslip = PayrollCalculatorV2.Calculate(driver, period, [], []);
        payslip.Finalise(DateTimeOffset.UtcNow);
        _repository.Payslips[payslip.Id] = payslip;

        var adminUser = new FakeCurrentUser(role: UserRole.Admin);
        var handler = new GetPayslipByIdQueryHandler(_repository, adminUser);

        var result = await handler.Handle(new GetPayslipByIdQuery(payslip.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payslip.Id, result.Value.Id);
        Assert.Equal("DRV-001", result.Value.EmployeeNo);
    }
}
