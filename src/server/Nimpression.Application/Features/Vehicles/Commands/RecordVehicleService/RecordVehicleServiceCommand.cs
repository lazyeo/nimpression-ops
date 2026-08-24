using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Vehicles.Commands.RecordVehicleService;

/// <summary>
/// 记录车辆保养命令。
/// </summary>
public sealed record RecordVehicleServiceCommand(
    Guid Id,
    decimal ServiceOdometerKm) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Vehicle";
    public Guid? AuditEntityId => Id;
    public string AuditAction => "RecordVehicleService";
}
