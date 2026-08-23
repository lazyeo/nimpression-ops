using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Timesheet;

/// <summary>
/// 班次打卡聚合根。记录司机上下班时间、GPS坐标、休息时长与管理员审计更正记录。
/// </summary>
public sealed class ShiftEntry : AggregateRoot
{
    public Guid DriverId { get; private set; }
    public DateTimeOffset ClockInAt { get; private set; }
    public decimal? ClockInLat { get; private set; }
    public decimal? ClockInLng { get; private set; }
    public DateTimeOffset? ClockOutAt { get; private set; }
    public decimal? ClockOutLat { get; private set; }
    public decimal? ClockOutLng { get; private set; }
    public Guid? VehicleId { get; private set; }
    public int BreakMinutes { get; private set; }
    public string? Note { get; private set; }
    public ShiftStatus Status { get; private set; }
    public string? AdminCorrectionReason { get; private set; }
    public Guid? CorrectedByUserId { get; private set; }
    public DateTimeOffset? CorrectedAt { get; private set; }

    private ShiftEntry()
    {
    }

    public ShiftEntry(
        Guid id,
        Guid driverId,
        DateTimeOffset clockInAt,
        decimal? clockInLat = null,
        decimal? clockInLng = null,
        Guid? vehicleId = null) : base(id)
    {
        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("DriverId cannot be empty.");
        }

        DriverId = driverId;
        ClockInAt = clockInAt;
        ClockInLat = clockInLat;
        ClockInLng = clockInLng;
        VehicleId = vehicleId;
        BreakMinutes = 0;
        Status = ShiftStatus.Active;
    }

    public void ClockOut(
        DateTimeOffset clockOutAt,
        decimal? clockOutLat = null,
        decimal? clockOutLng = null,
        int breakMinutes = 0,
        string? note = null)
    {
        if (Status != ShiftStatus.Active)
        {
            throw new DomainValidationException($"Cannot clock out of a shift in '{Status}' status.");
        }

        if (clockOutAt < ClockInAt)
        {
            throw new DomainValidationException(
                $"Clock out time ({clockOutAt}) cannot be earlier than clock in time ({ClockInAt}).");
        }

        if (breakMinutes < 0)
        {
            throw new DomainValidationException("Break minutes cannot be negative.");
        }

        ClockOutAt = clockOutAt;
        ClockOutLat = clockOutLat;
        ClockOutLng = clockOutLng;
        BreakMinutes = breakMinutes;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Status = ShiftStatus.Completed;
    }

    public void AdminCorrect(
        DateTimeOffset newClockInAt,
        DateTimeOffset? newClockOutAt,
        int newBreakMinutes,
        string reason,
        Guid adminUserId,
        DateTimeOffset correctedAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Admin correction reason is mandatory.");
        }

        if (adminUserId == Guid.Empty)
        {
            throw new DomainValidationException("Admin UserId cannot be empty.");
        }

        if (newClockOutAt.HasValue && newClockOutAt.Value < newClockInAt)
        {
            throw new DomainValidationException("Clock out time cannot be earlier than clock in time.");
        }

        if (newBreakMinutes < 0)
        {
            throw new DomainValidationException("Break minutes cannot be negative.");
        }

        ClockInAt = newClockInAt;
        ClockOutAt = newClockOutAt;
        BreakMinutes = newBreakMinutes;
        AdminCorrectionReason = reason.Trim();
        CorrectedByUserId = adminUserId;
        CorrectedAt = correctedAt;

        if (newClockOutAt.HasValue && Status == ShiftStatus.Active)
        {
            Status = ShiftStatus.Completed;
        }
    }

    public WorkHours CalculateWorkHours()
    {
        if (!ClockOutAt.HasValue)
        {
            return WorkHours.Zero;
        }

        var totalMinutes = (ClockOutAt.Value - ClockInAt).TotalMinutes;
        var payableMinutes = Math.Max(0, totalMinutes - BreakMinutes);
        return WorkHours.FromMinutes((int)Math.Round(payableMinutes));
    }
}
