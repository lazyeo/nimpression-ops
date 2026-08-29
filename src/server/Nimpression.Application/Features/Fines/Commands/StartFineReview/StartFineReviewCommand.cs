using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Fines.Commands.StartFineReview;

/// <summary>
/// 开始审核交通罚单命令（F8.2: Submitted -> UnderReview）。
/// </summary>
public sealed record StartFineReviewCommand(Guid FineId) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Fine";
    public Guid? AuditEntityId => FineId;
    public string AuditAction => "StartFineReview";
}
