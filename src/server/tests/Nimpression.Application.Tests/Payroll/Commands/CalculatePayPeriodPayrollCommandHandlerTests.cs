using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.Commands.CalculatePayPeriodPayroll;
using Nimpression.Application.Tests.Payroll.TestDoubles;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Xunit;

namespace Nimpression.Application.Tests.Payroll.Commands;

public sealed class CalculatePayPeriodPayrollCommandHandlerTests
{
    private readonly FakePayrollRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditSink _auditSink = new();
    private readonly FakeCurrentUser _currentUser = new(role: UserRole.Admin);
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero));

    private static Driver CreateDriver(string empNo = "DRV-001", decimal hourly = 30m, decimal perTrip = 40m, decimal perKm = 1m)
    {
        return new Driver(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            employeeNo: empNo,
            licenceClass: "Class 2",
            licenceExpiry: new DateOnly(2028, 1, 1),
            hourlyRate: new Money(hourly),
            perTripRate: new Money(perTrip),
            perKmRate: new Money(perKm),
            phoneEnc: "phone",
            addressEnc: "addr",
            emergencyContactEnc: "emrg",
            hiredOn: new DateOnly(2025, 1, 1));
    }

    [Fact]
    public async Task F7_8_CalculatePayroll_IsRepeatable_AndOverwritesDraft()
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        _repository.PayPeriods[period.Id] = period;

        var driver = CreateDriver();
        _repository.Drivers[driver.Id] = driver;

        var nzOffset = TimeSpan.FromHours(12);
        var shift = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset));
        shift.ClockOut(new DateTimeOffset(2026, 8, 17, 16, 0, 0, nzOffset)); // 8h
        _repository.Shifts.Add(shift);

        var handler = new CalculatePayPeriodPayrollCommandHandler(
            _repository, _unitOfWork, _currentUser, _auditSink, _dateTimeProvider);

        var command = new CalculatePayPeriodPayrollCommand(period.Id);

        // First calculation
        var result1 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result1.IsSuccess);
        Assert.Single(result1.Value);
        Assert.Equal(240.00m, result1.Value[0].GrossPay);
        Assert.Single(_repository.Payslips);

        // Add another shift (8h on day 2) and re-calculate
        var shift2 = new ShiftEntry(Guid.NewGuid(), driver.Id, new DateTimeOffset(2026, 8, 18, 8, 0, 0, nzOffset));
        shift2.ClockOut(new DateTimeOffset(2026, 8, 18, 16, 0, 0, nzOffset)); // 8h
        _repository.Shifts.Add(shift2);

        // Second calculation (repeatable trial calculation)
        var result2 = await handler.Handle(command, CancellationToken.None);
        Assert.True(result2.IsSuccess);
        Assert.Single(result2.Value);
        Assert.Equal(480.00m, result2.Value[0].GrossPay); // 16h * $30 = $480
        Assert.Single(_repository.Payslips); // Replaced, exactly 1 payslip exists
    }

    [Fact]
    public async Task CalculatePayroll_PeriodFinalised_ReturnsUnprocessable()
    {
        var period = new PayPeriod(Guid.NewGuid(), new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 30));
        period.Finalise(DateTimeOffset.UtcNow);
        _repository.PayPeriods[period.Id] = period;

        var handler = new CalculatePayPeriodPayrollCommandHandler(
            _repository, _unitOfWork, _currentUser, _auditSink, _dateTimeProvider);

        var command = new CalculatePayPeriodPayrollCommand(period.Id);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.UnprocessableEntity, result.Error!.Kind);
        Assert.Equal("period_finalised", result.Error.Code);
    }
}
