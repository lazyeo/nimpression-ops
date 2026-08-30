using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Application.Features.Notifications.PartnerContacts.Commands.ActivatePartnerContact;

/// <summary>
/// 启用外部伙伴联系人命令（F11.1）。
/// </summary>
public sealed record ActivatePartnerContactCommand(Guid Id) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "PartnerContact.Activated";
    public string AuditEntityType => "PartnerContact";
    public Guid? AuditEntityId => Id;
}

public sealed class ActivatePartnerContactCommandHandler(
    IPartnerContactRepository partnerContactRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ActivatePartnerContactCommand, Result>
{
    public async Task<Result> Handle(ActivatePartnerContactCommand request, CancellationToken cancellationToken)
    {
        var partnerContact = await partnerContactRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partnerContact is null)
        {
            return Error.NotFound("partner_contact_not_found", $"Partner contact with ID '{request.Id}' was not found.");
        }

        partnerContact.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
