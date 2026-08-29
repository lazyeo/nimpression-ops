using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.Commands.CalculatePayPeriodPayroll;
using Nimpression.Application.Features.Payroll.Commands.FinalisePayPeriod;
using Nimpression.Application.Features.Payroll.Commands.VoidPayPeriod;
using Nimpression.Application.Tests.Payroll.TestDoubles;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Payroll.Commands;

public sealed class VoidPayPeriodCommandHandlerTests
{
    private readonly FakePayrollRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditSink _auditSink = new();
    private readonly FakeCurrentUser _currentUser = new(role: UserRole.Admin);
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero));

    private static Driver CreateDriver(string empNo = "DRV-001")
    {
        return new Driver(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
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
    public async Task F7_8_VoidPayPeriod_ClearsPayslipsAndReopensPeriod_WithAudit()
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var driver = CreateDriver();
        _repository.Drivers[driver.Id] = driver;

        var nzOffset = TimeSpan.FromHours(12);
        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 17, 16, 0, 0, nzOffset)); // 8h
        _repository.Shifts.Add(shift);

        // 1. Calculate & Finalise
        var calcHandler = new CalculatePayPeriodPayrollCommandHandler(
            _repository, _unitOfWork, _currentUser, _auditSink, _dateTimeProvider);
        await calcHandler.Handle(new CalculatePayPeriodPayrollCommand(period.Id), CancellationToken.None);

        var finaliseHandler = new FinalisePayPeriodCommandHandler(
            _repository, _unitOfWork, _currentUser, _auditSink, _dateTimeProvider);
        await finaliseHandler.Handle(new FinalisePayPeriodCommand(period.Id), CancellationToken.None);

        Assert.Equal(PayPeriodStatus.Finalised, period.Status);
        Assert.Single(_repository.Payslips);

        // 2. Void and reopen
        var voidHandler = new VoidPayPeriodCommandHandler(
            _repository, _unitOfWork, _currentUser, _auditSink);

        var voidCommand = new VoidPayPeriodCommand(period.Id, "Time tracking correction needed");
        var voidResult = await voidHandler.Handle(voidCommand, CancellationToken.None);

        Assert.True(voidResult.IsSuccess);
        Assert.Equal(PayPeriodStatus.Open, voidResult.Value.Status);
        Assert.Empty(_repository.Payslips); // Old payslips cleared
        Assert.Contains(_auditSink.RecordedAudits, a => a.Action == "VoidPayPeriod" && a.AfterJson!.Contains("Time tracking correction needed"));
    }

    [Fact]
    public async Task VoidPayPeriod_EmptyReason_ReturnsUnprocessable()
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var voidHandler = new VoidPayPeriodCommandHandler(
            _repository, _unitOfWork, _currentUser, _auditSink);

        var voidCommand = new VoidPayPeriodCommand(period.Id, "   ");
        var result = await voidHandler.Handle(voidCommand, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.UnprocessableEntity, result.Error!.Kind);
        Assert.Equal("reason_required", result.Error.Code);
    }
}
