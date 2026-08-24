using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Areas.Commands.CreateArea;

/// <summary>
/// 创建运营区域命令。
/// </summary>
public sealed record CreateAreaCommand(
    string Name,
    string Code,
    string? Description = null,
    string? GeoJson = null,
    bool IsActive = true) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Area";
    public Guid? AuditEntityId => null;
    public string AuditAction => "CreateArea";
}
