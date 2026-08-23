using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Tests.Services;

public sealed class ShiftDurationCalculatorTests
{
    [Fact]
    public void ShiftDuration_cross_midnight_attributes_to_clock_in_date()
    {
        // 22:00 on 2026-08-20 to 06:00 on 2026-08-21 (NZ time +12)
        var offset = TimeSpan.FromHours(12);
        var clockIn = new DateTimeOffset(2026, 8, 20, 22, 0, 0, offset);
        var clockOut = new DateTimeOffset(2026, 8, 21, 6, 0, 0, offset);

        var result = ShiftDurationCalculator.Calculate(clockIn, clockOut, breakMinutes: 0);

        Assert.Equal(new DateOnly(2026, 8, 20), result.AttributedDate);
        Assert.Equal(TimeSpan.FromHours(8), result.RawDuration);
        Assert.Equal(new WorkHours(8.00m), result.PayableHours);
    }

    [Fact]
    public void ShiftDuration_deducts_break_minutes()
    {
        var offset = TimeSpan.FromHours(12);
        var clockIn = new DateTimeOffset(2026, 8, 20, 8, 0, 0, offset);
        var clockOut = new DateTimeOffset(2026, 8, 20, 17, 0, 0, offset); // 9 hours raw

        var result = ShiftDurationCalculator.Calculate(clockIn, clockOut, breakMinutes: 45);

        Assert.Equal(new DateOnly(2026, 8, 20), result.AttributedDate);
        Assert.Equal(TimeSpan.FromHours(9), result.RawDuration);
        Assert.Equal(45, result.BreakMinutes);
        // (540 - 45) / 60 = 495 / 60 = 8.25h
        Assert.Equal(new WorkHours(8.25m), result.PayableHours);
    }

    [Fact]
    public void ShiftDuration_dst_spring_forward_transition_2026_09_27()
    {
        // 2026-09-27 in NZ: at 02:00 clocks jump to 03:00 (+12 -> +13)
        // Shift from 01:00 (+12) to 05:00 (+13)
        var clockIn = new DateTimeOffset(2026, 9, 27, 1, 0, 0, TimeSpan.FromHours(12));
        var clockOut = new DateTimeOffset(2026, 9, 27, 5, 0, 0, TimeSpan.FromHours(13));

        var result = ShiftDurationCalculator.Calculate(clockIn, clockOut, breakMinutes: 0);

        Assert.Equal(new DateOnly(2026, 9, 27), result.AttributedDate);
        // 01:00+12 = 13:00 UTC (prev day); 05:00+13 = 16:00 UTC (prev day) -> diff is 3 hours
        Assert.Equal(TimeSpan.FromHours(3), result.RawDuration);
        Assert.Equal(new WorkHours(3.00m), result.PayableHours);
    }

    [Fact]
    public void ShiftDuration_dst_fall_back_transition_2026_04_05()
    {
        // 2026-04-05 in NZ: at 03:00 clocks fall back to 02:00 (+13 -> +12)
        // Shift from 01:00 (+13) to 05:00 (+12)
        var clockIn = new DateTimeOffset(2026, 4, 5, 1, 0, 0, TimeSpan.FromHours(13));
        var clockOut = new DateTimeOffset(2026, 4, 5, 5, 0, 0, TimeSpan.FromHours(12));

        var result = ShiftDurationCalculator.Calculate(clockIn, clockOut, breakMinutes: 0);

        Assert.Equal(new DateOnly(2026, 4, 5), result.AttributedDate);
        // 01:00+13 = 12:00 UTC (prev day); 05:00+12 = 17:00 UTC (prev day) -> diff is 5 hours
        Assert.Equal(TimeSpan.FromHours(5), result.RawDuration);
        Assert.Equal(new WorkHours(5.00m), result.PayableHours);
    }

    [Fact]
    public void ShiftDuration_from_shift_entry_and_guards()
    {
        var shift = new ShiftEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.FromHours(12)));

        Assert.Throws<DomainValidationException>(() => ShiftDurationCalculator.Calculate(shift));

        shift.ClockOut(new DateTimeOffset(2026, 8, 20, 16, 30, 0, TimeSpan.FromHours(12)), breakMinutes: 30);
        var result = ShiftDurationCalculator.Calculate(shift);

        Assert.Equal(new DateOnly(2026, 8, 20), result.AttributedDate);
        Assert.Equal(new WorkHours(8.00m), result.PayableHours);

        Assert.Throws<DomainValidationException>(() =>
            ShiftDurationCalculator.Calculate(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(-1), 0));

        Assert.Throws<DomainValidationException>(() =>
            ShiftDurationCalculator.Calculate(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), -5));
    }
}
