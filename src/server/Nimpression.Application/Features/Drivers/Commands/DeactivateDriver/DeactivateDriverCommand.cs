using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Drivers.Commands.DeactivateDriver;

/// <summary>
/// 管理员停用司机命令（F2.1）。
/// </summary>
public sealed record DeactivateDriverCommand(
    Guid DriverId,
    string? Reason = null) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "Driver";
    public Guid? AuditEntityId => DriverId;
    public string AuditAction => "DeactivateDriver";
}
