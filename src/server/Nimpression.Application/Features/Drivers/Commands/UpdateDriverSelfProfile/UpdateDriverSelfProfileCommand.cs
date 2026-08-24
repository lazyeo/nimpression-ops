using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.Commands.UpdateDriverSelfProfile;

/// <summary>
/// 司机个人资料自助修改命令（F2.4）。
/// 司机仅可修改手机号、紧急联系人、语言偏好；
/// 若携带工号、费率、状态等敏感字段则直接返回 403。
/// </summary>
public sealed record UpdateDriverSelfProfileCommand(
    Guid DriverId,
    string Phone,
    string EmergencyContact,
    string Locale = "en-NZ",
    string? Address = null,
    string? AttemptedEmployeeNo = null,
    decimal? AttemptedHourlyRate = null,
    decimal? AttemptedPerTripRate = null,
    decimal? AttemptedPerKmRate = null,
    DriverStatus? AttemptedStatus = null,
    DateOnly? AttemptedLicenceExpiry = null) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Driver";
    public Guid? AuditEntityId => DriverId;
    public string AuditAction => "UpdateSelfProfile";
}
