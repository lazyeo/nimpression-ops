using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Vehicles.Commands.ReleaseVehicleAssignment;

/// <summary>
/// 释放车辆分派（交车/还车）命令。
/// </summary>
public sealed record ReleaseVehicleAssignmentCommand(
    Guid AssignmentId,
    DateTimeOffset? ReleasedAt = null) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "VehicleAssignment";
    public Guid? AuditEntityId => AssignmentId;
    public string AuditAction => "ReleaseVehicleAssignment";
}
