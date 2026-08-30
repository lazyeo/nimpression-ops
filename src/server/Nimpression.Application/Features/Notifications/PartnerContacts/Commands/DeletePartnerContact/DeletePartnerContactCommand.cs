using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Application.Features.Notifications.PartnerContacts.Commands.DeletePartnerContact;

/// <summary>
/// 删除外部伙伴联系人命令（F11.1）。
/// </summary>
public sealed record DeletePartnerContactCommand(Guid Id) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "PartnerContact.Deleted";
    public string AuditEntityType => "PartnerContact";
    public Guid? AuditEntityId => Id;
}

public sealed class DeletePartnerContactCommandHandler(
    IPartnerContactRepository partnerContactRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeletePartnerContactCommand, Result>
{
    public async Task<Result> Handle(DeletePartnerContactCommand request, CancellationToken cancellationToken)
    {
        var partnerContact = await partnerContactRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partnerContact is null)
        {
            return Error.NotFound("partner_contact_not_found", $"Partner contact with ID '{request.Id}' was not found.");
        }

        partnerContactRepository.Remove(partnerContact);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
