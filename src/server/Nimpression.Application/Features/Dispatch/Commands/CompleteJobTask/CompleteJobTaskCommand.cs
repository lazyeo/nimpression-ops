using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Dispatch.Commands.CompleteJobTask;

/// <summary>
/// 完成派发任务命令（F5.2 / F5.3）。
/// </summary>
public sealed record CompleteJobTaskCommand(
    Guid TaskId,
    DateTimeOffset? CompletedAt = null,
    decimal? ActualDistanceKm = null,
    decimal? EndOdometerKm = null) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "JobTask";
    public Guid? AuditEntityId => TaskId;
    public string AuditAction => "CompleteJobTask";
}
