using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Vehicles.Commands.CreateVehicle;

/// <summary>
/// 创建车辆命令。
/// </summary>
public sealed record CreateVehicleCommand(
    string Rego,
    string Make,
    string Model,
    int Year,
    string VinEnc,
    decimal OdometerKm,
    decimal ServiceIntervalKm,
    decimal? LastServiceOdometerKm = null,
    DateOnly? WofExpiry = null,
    DateOnly? CofExpiry = null,
    DateOnly? InsuranceExpiry = null,
    VehicleStatus Status = VehicleStatus.Active) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Vehicle";
    public Guid? AuditEntityId => null;
    public string AuditAction => "CreateVehicle";
}
