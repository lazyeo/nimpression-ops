using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Fines.Commands.AcceptFine;

/// <summary>
/// 接受交通罚单命令（F8.2 / F8.3: UnderReview -> Accepted，并触发 FineAccepted 领域事件）。
/// </summary>
public sealed record AcceptFineCommand(Guid FineId, string? ReviewNote = null)
    : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Fine";
    public Guid? AuditEntityId => FineId;
    public string AuditAction => "AcceptFine";
}
