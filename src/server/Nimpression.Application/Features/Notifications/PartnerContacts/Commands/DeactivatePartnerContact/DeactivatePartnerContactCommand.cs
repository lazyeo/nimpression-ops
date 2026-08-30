using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Application.Features.Notifications.PartnerContacts.Commands.DeactivatePartnerContact;

/// <summary>
/// 停用外部伙伴联系人命令（F11.1：停用后不再接收邮件）。
/// </summary>
public sealed record DeactivatePartnerContactCommand(Guid Id) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "PartnerContact.Deactivated";
    public string AuditEntityType => "PartnerContact";
    public Guid? AuditEntityId => Id;
}

public sealed class DeactivatePartnerContactCommandHandler(
    IPartnerContactRepository partnerContactRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivatePartnerContactCommand, Result>
{
    public async Task<Result> Handle(DeactivatePartnerContactCommand request, CancellationToken cancellationToken)
    {
        var partnerContact = await partnerContactRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partnerContact is null)
        {
            return Error.NotFound("partner_contact_not_found", $"Partner contact with ID '{request.Id}' was not found.");
        }

        partnerContact.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
