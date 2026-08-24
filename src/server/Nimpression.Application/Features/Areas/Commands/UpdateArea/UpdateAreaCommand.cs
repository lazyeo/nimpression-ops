using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Areas.Commands.UpdateArea;

/// <summary>
/// 更新运营区域命令。
/// </summary>
public sealed record UpdateAreaCommand(
    Guid Id,
    string Name,
    string Code,
    string? Description = null,
    string? GeoJson = null,
    bool IsActive = true) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Area";
    public Guid? AuditEntityId => Id;
    public string AuditAction => "UpdateArea";
}
