using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.Commands.UpdateDriver;

/// <summary>
/// 管理员更新司机信息命令（F2.1）。
/// </summary>
public sealed record UpdateDriverCommand(
    Guid DriverId,
    string DisplayName,
    string LicenceClass,
    DateOnly LicenceExpiry,
    decimal HourlyRateAmount,
    string HourlyRateCurrency,
    decimal PerTripRateAmount,
    string PerTripRateCurrency,
    decimal PerKmRateAmount,
    string PerKmRateCurrency,
    string Phone,
    string Address,
    string EmergencyContact,
    DriverStatus Status) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Driver";
    public Guid? AuditEntityId => DriverId;
    public string AuditAction => "UpdateDriver";
}
