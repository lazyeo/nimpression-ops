using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Timesheets.Commands.ClockOut;

/// <summary>
/// 下班打卡命令（F6.1）。记录下班时间、GPS坐标、休息分钟数与备注。
/// </summary>
public sealed record ClockOutCommand(
    Guid? ShiftId = null,
    Guid? DriverId = null,
    DateTimeOffset? ClockOutAt = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    int BreakMinutes = 0,
    string? Note = null,
    bool LocationUnavailable = false) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    internal Guid? TargetShiftId { get; set; }

    public string AuditEntityType => "ShiftEntry";
    public Guid? AuditEntityId => ShiftId ?? TargetShiftId;
    public string AuditAction => "ClockOut";
}
