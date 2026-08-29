using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Areas.Commands.EndAreaAssignment;

/// <summary>
/// 结束司机区域分配命令。
/// </summary>
public sealed record EndAreaAssignmentCommand(
    Guid AssignmentId,
    DateOnly EffectiveTo) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "AreaAssignment";
    public Guid? AuditEntityId => AssignmentId;
    public string AuditAction => "EndAreaAssignment";
}
