using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Fines.Commands.WaiveFine;

/// <summary>
/// 减免/撤销交通罚单命令（F8.2: UnderReview -> Waived）。
/// </summary>
public sealed record WaiveFineCommand(Guid FineId, string ReviewNote)
    : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Fine";
    public Guid? AuditEntityId => FineId;
    public string AuditAction => "WaiveFine";
}
