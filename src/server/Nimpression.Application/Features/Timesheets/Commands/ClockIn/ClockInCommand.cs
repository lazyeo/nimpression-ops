using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Timesheets.Commands.ClockIn;

/// <summary>
/// 上班打卡命令（F6.1）。记录打卡时间、GPS坐标（支持无坐标降级）与可选车辆ID。
/// </summary>
public sealed record ClockInCommand(
    Guid? DriverId = null,
    DateTimeOffset? ClockInAt = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    Guid? VehicleId = null,
    bool LocationUnavailable = false) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    internal Guid? CreatedId { get; set; }

    public string AuditEntityType => "ShiftEntry";
    public Guid? AuditEntityId => CreatedId;
    public string AuditAction => "ClockIn";
}
