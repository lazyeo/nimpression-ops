using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Dispatch.Commands.AcknowledgeJobTask;

/// <summary>
/// 司机确认派发任务命令（F5.2 / F5.3）。
/// </summary>
public sealed record AcknowledgeJobTaskCommand(
    Guid TaskId,
    DateTimeOffset? AcknowledgedAt = null) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "JobTask";
    public Guid? AuditEntityId => TaskId;
    public string AuditAction => "AcknowledgeJobTask";
}
