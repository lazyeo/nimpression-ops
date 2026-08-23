using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Identity.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "User";
    public Guid? AuditEntityId => UserId;
    public string AuditAction => "ChangePassword";
}
