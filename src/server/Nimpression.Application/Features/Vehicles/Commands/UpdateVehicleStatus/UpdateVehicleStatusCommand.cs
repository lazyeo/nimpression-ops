using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Vehicles.Commands.UpdateVehicleStatus;

/// <summary>
/// 更新车辆运营/保养状态命令。
/// </summary>
public sealed record UpdateVehicleStatusCommand(
    Guid Id,
    VehicleStatus Status) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Vehicle";
    public Guid? AuditEntityId => Id;
    public string AuditAction => "UpdateVehicleStatus";
}
