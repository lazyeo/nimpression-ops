using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Areas.Commands.AssignDriverToArea;

/// <summary>
/// 为司机分配区域命令（F4.2）。
/// </summary>
public sealed record AssignDriverToAreaCommand(
    Guid DriverId,
    Guid AreaId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo = null) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "AreaAssignment";
    public Guid? AuditEntityId => null;
    public string AuditAction => "AssignDriverToArea";
}
