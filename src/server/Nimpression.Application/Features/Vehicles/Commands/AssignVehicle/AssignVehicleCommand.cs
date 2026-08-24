using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Vehicles.Commands.AssignVehicle;

/// <summary>
/// 指派车辆给司机命令。
/// </summary>
public sealed record AssignVehicleCommand(
    Guid VehicleId,
    Guid DriverId,
    DateTimeOffset? AssignedAt = null) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "VehicleAssignment";
    public Guid? AuditEntityId => null;
    public string AuditAction => "AssignVehicle";
}
