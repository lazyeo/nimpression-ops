using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;

namespace Nimpression.Application.Features.Timesheets.DTOs;

/// <summary>
/// 班次打卡记录详情 DTO。
/// </summary>
public sealed record ShiftEntryDto(
    Guid Id,
    Guid DriverId,
    string? DriverName,
    DateTimeOffset ClockInAt,
    decimal? ClockInLat,
    decimal? ClockInLng,
    bool LocationUnavailable,
    DateTimeOffset? ClockOutAt,
    decimal? ClockOutLat,
    decimal? ClockOutLng,
    Guid? VehicleId,
    int BreakMinutes,
    string? Note,
    ShiftStatus Status,
    DateOnly? AttributedDate,
    decimal? RawDurationHours,
    decimal? PayableHours,
    string? AdminCorrectionReason,
    Guid? CorrectedByUserId,
    DateTimeOffset? CorrectedAt)
{
    public static ShiftEntryDto FromEntity(ShiftEntry entity, string? driverName = null)
    {
        DateOnly? attributedDate = null;
        decimal? rawDurationHours = null;
        decimal? payableHours = null;

        if (entity.ClockOutAt.HasValue)
        {
            var calc = ShiftDurationCalculator.Calculate(entity.ClockInAt, entity.ClockOutAt.Value, entity.BreakMinutes);
            attributedDate = calc.AttributedDate;
            rawDurationHours = (decimal)calc.RawDuration.TotalHours;
            payableHours = calc.PayableHours.Value;
        }
        else
        {
            attributedDate = Nimpression.Domain.Common.NzTimeZone.ToNzDateOnly(entity.ClockInAt);
        }

        var locationUnavailable = !entity.ClockInLat.HasValue && !entity.ClockInLng.HasValue;

        return new ShiftEntryDto(
            entity.Id,
            entity.DriverId,
            driverName,
            entity.ClockInAt,
            entity.ClockInLat,
            entity.ClockInLng,
            locationUnavailable,
            entity.ClockOutAt,
            entity.ClockOutLat,
            entity.ClockOutLng,
            entity.VehicleId,
            entity.BreakMinutes,
            entity.Note,
            entity.Status,
            attributedDate,
            rawDurationHours,
            payableHours,
            entity.AdminCorrectionReason,
            entity.CorrectedByUserId,
            entity.CorrectedAt);
    }
}
