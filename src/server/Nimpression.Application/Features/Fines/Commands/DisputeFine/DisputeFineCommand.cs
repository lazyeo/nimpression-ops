using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Fines.Commands.DisputeFine;

/// <summary>
/// 争议交通罚单命令（F8.2: UnderReview -> Disputed）。
/// </summary>
public sealed record DisputeFineCommand(Guid FineId, string ReviewNote)
    : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Fine";
    public Guid? AuditEntityId => FineId;
    public string AuditAction => "DisputeFine";
}
