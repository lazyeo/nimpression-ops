using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Vehicles.Commands.UpdateVehicle;

/// <summary>
/// 更新车辆基本信息与合规日期命令。
/// </summary>
public sealed record UpdateVehicleCommand(
    Guid Id,
    DateOnly? WofExpiry,
    DateOnly? CofExpiry,
    DateOnly? InsuranceExpiry,
    VehicleStatus Status) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Vehicle";
    public Guid? AuditEntityId => Id;
    public string AuditAction => "UpdateVehicle";
}
