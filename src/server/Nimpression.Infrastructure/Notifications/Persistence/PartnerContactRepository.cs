using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Notifications.Persistence;

/// <summary>
/// 外部伙伴联系人 EF Core 仓储实现（F11.1）。
/// 列表查询均采用 Select 投影，消除实体映射开销与 N+1 问题。
/// </summary>
public sealed class PartnerContactRepository(AppDbContext dbContext) : IPartnerContactRepository
{
    public async Task<PartnerContact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.PartnerContacts
            .FirstOrDefaultAsync(pc => pc.Id == id, cancellationToken);
    }

    public async Task<List<PartnerContact>> GetActiveByKindAsync(PartnerKind kind, CancellationToken cancellationToken = default)
    {
        return await dbContext.PartnerContacts
            .Where(pc => pc.Kind == kind && pc.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<PartnerContactDto>> GetListAsync(PartnerContactFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.PartnerContacts.AsNoTracking().AsQueryable();

        if (filter.Kind.HasValue)
        {
            query = query.Where(pc => pc.Kind == filter.Kind.Value);
        }

        if (filter.Active.HasValue)
        {
            query = query.Where(pc => pc.Active == filter.Active.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = $"%{filter.SearchTerm.Trim()}%";
            query = query.Where(pc =>
                EF.Functions.ILike(pc.CompanyName, search) ||
                EF.Functions.ILike((string)(object)pc.Email, search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(pc => pc.Kind)
            .ThenBy(pc => pc.CompanyName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(pc => new PartnerContactDto(
                pc.Id,
                pc.Kind,
                pc.CompanyName,
                pc.Email.Value,
                pc.Active))
            .ToListAsync(cancellationToken);

        return new PagedResult<PartnerContactDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task AddAsync(PartnerContact partnerContact, CancellationToken cancellationToken = default)
    {
        await dbContext.PartnerContacts.AddAsync(partnerContact, cancellationToken);
    }

    public void Remove(PartnerContact partnerContact)
    {
        dbContext.PartnerContacts.Remove(partnerContact);
    }
}
