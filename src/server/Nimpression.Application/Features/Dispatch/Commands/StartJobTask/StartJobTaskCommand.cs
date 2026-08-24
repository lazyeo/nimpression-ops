using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Dispatch.Commands.StartJobTask;

/// <summary>
/// 司机开始执行任务命令（F5.2 / F5.3）。
/// </summary>
public sealed record StartJobTaskCommand(
    Guid TaskId,
    DateTimeOffset? StartedAt = null,
    decimal? StartOdometerKm = null) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "JobTask";
    public Guid? AuditEntityId => TaskId;
    public string AuditAction => "StartJobTask";
}
