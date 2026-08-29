using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Areas.Commands.DeleteArea;

/// <summary>
/// 删除运营区域命令。
/// </summary>
public sealed record DeleteAreaCommand(Guid Id) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Area";
    public Guid? AuditEntityId => Id;
    public string AuditAction => "DeleteArea";
}
