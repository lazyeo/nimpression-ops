using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Vehicles.Commands.RecordOdometerReading;

/// <summary>
/// 上报车辆里程读数命令。
/// </summary>
public sealed record RecordOdometerReadingCommand(
    Guid VehicleId,
    Guid DriverId,
    decimal ReadingKm,
    string? PhotoKey = null,
    DateTimeOffset? RecordedAt = null,
    string Source = "DriverApp") : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "OdometerReading";
    public Guid? AuditEntityId => null;
    public string AuditAction => "RecordOdometerReading";
}
