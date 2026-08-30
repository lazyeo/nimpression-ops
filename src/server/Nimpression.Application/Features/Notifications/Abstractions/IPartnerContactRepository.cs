using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Notifications.Abstractions;

/// <summary>
/// 外部伙伴联系人仓储接口（F11.1）。
/// </summary>
public interface IPartnerContactRepository
{
    Task<PartnerContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<PartnerContact>> GetActiveByKindAsync(PartnerKind kind, CancellationToken cancellationToken = default);

    Task<PagedResult<PartnerContactDto>> GetListAsync(PartnerContactFilter filter, CancellationToken cancellationToken = default);

    Task AddAsync(PartnerContact partnerContact, CancellationToken cancellationToken = default);

    void Remove(PartnerContact partnerContact);
}
