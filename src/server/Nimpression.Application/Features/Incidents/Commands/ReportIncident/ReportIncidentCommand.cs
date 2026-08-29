using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Incidents.Commands.ReportIncident;

/// <summary>
/// 事故上报命令（F9.1: 司机/管理员均可提交，含时间、地点、严重度、描述、多图、第三方信息）。
/// </summary>
public sealed record ReportIncidentCommand(
    Guid? DriverId,
    Guid VehicleId,
    DateTimeOffset OccurredAt,
    string Location,
    IncidentSeverity Severity,
    string Description,
    List<string>? PhotoKeys = null,
    string? ThirdPartyInfo = null) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public Guid? CreatedId { get; set; }

    public string AuditEntityType => "IncidentReport";
    public Guid? AuditEntityId => CreatedId;
    public string AuditAction => "ReportIncident";
}
