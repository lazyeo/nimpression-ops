using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Dispatch.Commands.CancelJobTask;

/// <summary>
/// 取消派发任务命令（F5.3）。
/// </summary>
public sealed record CancelJobTaskCommand(
    Guid TaskId,
    string Reason,
    DateTimeOffset? CancelledAt = null) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "JobTask";
    public Guid? AuditEntityId => TaskId;
    public string AuditAction => "CancelJobTask";
}
