using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Notifications.Persistence;

/// <summary>
/// 邮件模板 EF Core 仓储实现（F11.2）。
/// 列表查询采用 Select 投影，消除 N+1 与映射开销。
/// </summary>
public sealed class EmailTemplateRepository(AppDbContext dbContext) : IEmailTemplateRepository
{
    public async Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.EmailTemplates
            .FirstOrDefaultAsync(et => et.Id == id, cancellationToken);
    }

    public async Task<EmailTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalizedKey = key.Trim().ToUpperInvariant();
        return await dbContext.EmailTemplates
            .FirstOrDefaultAsync(et => et.Key == normalizedKey, cancellationToken);
    }

    public async Task<PagedResult<EmailTemplateDto>> GetListAsync(EmailTemplateFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.EmailTemplates.AsNoTracking().AsQueryable();

        if (filter.Active.HasValue)
        {
            query = query.Where(et => et.Active == filter.Active.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = $"%{filter.SearchTerm.Trim()}%";
            query = query.Where(et =>
                EF.Functions.ILike(et.Key, search) ||
                EF.Functions.ILike(et.SubjectEn, search) ||
                EF.Functions.ILike(et.SubjectZh, search) ||
                EF.Functions.ILike(et.BodyEn, search) ||
                EF.Functions.ILike(et.BodyZh, search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(et => et.Key)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(et => new EmailTemplateDto(
                et.Id,
                et.Key,
                et.SubjectEn,
                et.SubjectZh,
                et.BodyEn,
                et.BodyZh,
                et.Active))
            .ToListAsync(cancellationToken);

        return new PagedResult<EmailTemplateDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task AddAsync(EmailTemplate template, CancellationToken cancellationToken = default)
    {
        await dbContext.EmailTemplates.AddAsync(template, cancellationToken);
    }
}
