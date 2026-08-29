using Nimpression.Application.Common.Results;
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

        // F7.11: 工时明细可追溯到 ShiftEntry，趟次明细可追溯到 JobTask
        Assert.Single(result.Value.ShiftDetails);
        Assert.Equal(shift.Id, result.Value.ShiftDetails[0].ShiftId);
        Assert.Equal(7.50m, result.Value.ShiftDetails[0].PayableHours);

        Assert.Single(result.Value.TripDetails);
        Assert.Equal(task.Id, result.Value.TripDetails[0].JobTaskId);
        Assert.Equal("TASK-001", result.Value.TripDetails[0].Ref);
        Assert.Equal(32.00m, result.Value.TripDetails[0].EffectiveDistanceKm);

        // F7.12: 工资单金额与罚款无计算关联；UI/API 分区展示并附法规说明
        Assert.Single(result.Value.Fines);
        Assert.Equal("INF-777888", result.Value.Fines[0].Reference);
        Assert.Equal(120.00m, result.Value.Fines[0].Amount);
        Assert.Contains("Wages Protection Act 1983", result.Value.FinesLegalNotice);
    }
}
