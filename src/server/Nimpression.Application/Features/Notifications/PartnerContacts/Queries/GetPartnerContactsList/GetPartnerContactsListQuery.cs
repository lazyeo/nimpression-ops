using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;

namespace Nimpression.Application.Features.Notifications.PartnerContacts.Queries.GetPartnerContactsList;

/// <summary>
/// 获取外部伙伴联系人列表查询（F11.1）。
/// </summary>
public sealed record GetPartnerContactsListQuery(PartnerContactFilter Filter) : IRequest<Result<PagedResult<PartnerContactDto>>>;

public sealed class GetPartnerContactsListQueryHandler(
    IPartnerContactRepository partnerContactRepository) : IRequestHandler<GetPartnerContactsListQuery, Result<PagedResult<PartnerContactDto>>>
{
    public async Task<Result<PagedResult<PartnerContactDto>>> Handle(GetPartnerContactsListQuery request, CancellationToken cancellationToken)
    {
        var result = await partnerContactRepository.GetListAsync(request.Filter, cancellationToken);
        return result;
    }
}
