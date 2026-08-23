using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Entities;

public sealed class ShiftEntryTests
{
    [Fact]
    public void ShiftEntry_initializes_active_and_records_clock_out()
    {
        var id = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var clockIn = new DateTimeOffset(2026, 8, 23, 8, 0, 0, TimeSpan.FromHours(12));

        var shift = new ShiftEntry(id, driverId, clockIn, -36.8485m, 174.7633m, vehicleId);

        Assert.Equal(ShiftStatus.Active, shift.Status);
        Assert.Equal(driverId, shift.DriverId);
        Assert.Equal(clockIn, shift.ClockInAt);
        Assert.Equal(-36.8485m, shift.ClockInLat);
        Assert.Equal(174.7633m, shift.ClockInLng);
        Assert.Equal(vehicleId, shift.VehicleId);
        Assert.Equal(WorkHours.Zero, shift.CalculateWorkHours());

        var clockOut = clockIn.AddHours(8.5);
        shift.ClockOut(clockOut, -36.8500m, 174.7600m, breakMinutes: 30, note: "Standard shift");

        Assert.Equal(ShiftStatus.Completed, shift.Status);
        Assert.Equal(clockOut, shift.ClockOutAt);
        Assert.Equal(30, shift.BreakMinutes);
        Assert.Equal("Standard shift", shift.Note);
        Assert.Equal(new WorkHours(8.00m), shift.CalculateWorkHours());
    }

    [Fact]
    public void ShiftEntry_clock_out_guards()
    {
        var clockIn = DateTimeOffset.UtcNow;
        var shift = new ShiftEntry(Guid.NewGuid(), Guid.NewGuid(), clockIn);

        // Cannot clock out before clock in
        Assert.Throws<DomainValidationException>(() => shift.ClockOut(clockIn.AddMinutes(-1)));

        // Cannot clock out with negative breaks
        Assert.Throws<DomainValidationException>(() => shift.ClockOut(clockIn.AddHours(4), breakMinutes: -10));

        // Successful clock out
        shift.ClockOut(clockIn.AddHours(4));

        // Cannot clock out again
        Assert.Throws<DomainValidationException>(() => shift.ClockOut(clockIn.AddHours(5)));
    }

    [Fact]
    public void ShiftEntry_admin_correction_records_reason_and_audit()
    {
        var clockIn = DateTimeOffset.UtcNow;
        var shift = new ShiftEntry(Guid.NewGuid(), Guid.NewGuid(), clockIn);

        var adminId = Guid.NewGuid();
        var correctedAt = DateTimeOffset.UtcNow;
        var newIn = clockIn.AddMinutes(-30);
        var newOut = clockIn.AddHours(8);

        shift.AdminCorrect(newIn, newOut, 45, "Driver forgot to clock in", adminId, correctedAt);

        Assert.Equal(newIn, shift.ClockInAt);
        Assert.Equal(newOut, shift.ClockOutAt);
        Assert.Equal(45, shift.BreakMinutes);
        Assert.Equal("Driver forgot to clock in", shift.AdminCorrectionReason);
        Assert.Equal(adminId, shift.CorrectedByUserId);
        Assert.Equal(correctedAt, shift.CorrectedAt);
        Assert.Equal(ShiftStatus.Completed, shift.Status);
    }

    [Fact]
    public void ShiftEntry_admin_correction_guards()
    {
        var shift = new ShiftEntry(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Reason is mandatory
        Assert.Throws<DomainValidationException>(() =>
            shift.AdminCorrect(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(4), 0, "", Guid.NewGuid(), DateTimeOffset.UtcNow));

        // Admin ID is mandatory
        Assert.Throws<DomainValidationException>(() =>
            shift.AdminCorrect(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(4), 0, "Reason", Guid.Empty, DateTimeOffset.UtcNow));

        // End < Start
        Assert.Throws<DomainValidationException>(() =>
            shift.AdminCorrect(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(-1), 0, "Reason", Guid.NewGuid(), DateTimeOffset.UtcNow));
    }
}
