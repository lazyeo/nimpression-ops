using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Dispatch.Commands.AssignJobTask;

/// <summary>
/// 指派任务给司机与车辆命令（F5.1 / F4.3）。
/// </summary>
public sealed record AssignJobTaskCommand(
    Guid TaskId,
    Guid DriverId,
    Guid VehicleId,
    DateTimeOffset? ScheduledFor = null,
    bool OverrideAreaWarning = false) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "JobTask";
    public Guid? AuditEntityId => TaskId;
    public string AuditAction => "AssignJobTask";
}
