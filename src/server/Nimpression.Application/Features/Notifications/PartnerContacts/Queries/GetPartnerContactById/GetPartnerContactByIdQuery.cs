using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;

namespace Nimpression.Application.Features.Notifications.PartnerContacts.Queries.GetPartnerContactById;

/// <summary>
/// 按 ID 获取外部伙伴联系人详情查询（F11.1）。
/// </summary>
public sealed record GetPartnerContactByIdQuery(Guid Id) : IRequest<Result<PartnerContactDto>>;

public sealed class GetPartnerContactByIdQueryHandler(
    IPartnerContactRepository partnerContactRepository) : IRequestHandler<GetPartnerContactByIdQuery, Result<PartnerContactDto>>
{
    public async Task<Result<PartnerContactDto>> Handle(GetPartnerContactByIdQuery request, CancellationToken cancellationToken)
    {
        var partnerContact = await partnerContactRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partnerContact is null)
        {
            return Error.NotFound("partner_contact_not_found", $"Partner contact with ID '{request.Id}' was not found.");
        }

        return new PartnerContactDto(
            partnerContact.Id,
            partnerContact.Kind,
            partnerContact.CompanyName,
            partnerContact.Email.Value,
            partnerContact.Active);
    }
}
