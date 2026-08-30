using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Notifications.Persistence;

/// <summary>
/// 邮件发送日志 EF Core 仓储实现（F11.5）。
/// 列表查询采用 Select 投影，消除 N+1 与映射开销。
/// </summary>
public sealed class EmailLogRepository(AppDbContext dbContext) : IEmailLogRepository
{
    public async Task<EmailLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.EmailLogs
            .FirstOrDefaultAsync(el => el.Id == id, cancellationToken);
    }

    public async Task<EmailLog?> GetByCorrelationIdAndRecipientAsync(string correlationId, string toAddress, CancellationToken cancellationToken = default)
    {
        var normalizedCorrId = correlationId.Trim();
        var email = new EmailAddress(toAddress.Trim());

        return await dbContext.EmailLogs
            .FirstOrDefaultAsync(el =>
                el.CorrelationId == normalizedCorrId &&
                el.ToAddress == email,
                cancellationToken);
    }

    public async Task<PagedResult<EmailLogDto>> GetListAsync(EmailLogFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.EmailLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();
            query = query.Where(el => el.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.TemplateKey))
        {
            var key = filter.TemplateKey.Trim().ToUpperInvariant();
            query = query.Where(el => el.TemplateKey == key);
        }

        if (!string.IsNullOrWhiteSpace(filter.ToAddress))
        {
            try
            {
                var emailVo = new EmailAddress(filter.ToAddress.Trim());
                query = query.Where(el => el.ToAddress == emailVo);
            }
            catch
            {
                // Invalid email string ignored in filter
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            var corrId = filter.CorrelationId.Trim();
            query = query.Where(el => el.CorrelationId == corrId);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(el => el.SentAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(el => el.SentAt <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = $"%{filter.SearchTerm.Trim()}%";
            query = query.Where(el =>
                EF.Functions.ILike(el.Subject, search) ||
                EF.Functions.ILike(el.CorrelationId, search) ||
                EF.Functions.ILike(el.TriggeredBy, search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(el => el.SentAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(el => el.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(el => new EmailLogDto(
                el.Id,
                el.TemplateKey,
                el.ToAddress.Value,
                el.Subject,
                el.Status,
                el.Attempts,
                el.LastError,
                el.SentAt,
                el.TriggeredBy,
                el.CorrelationId))
            .ToListAsync(cancellationToken);

        return new PagedResult<EmailLogDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<List<EmailLog>> GetRetryableLogsAsync(int maxAttempts, CancellationToken cancellationToken = default)
    {
        return await dbContext.EmailLogs
            .Where(el => el.Status == "Failed" && el.Attempts < maxAttempts)
            .OrderBy(el => el.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EmailLog emailLog, CancellationToken cancellationToken = default)
    {
        await dbContext.EmailLogs.AddAsync(emailLog, cancellationToken);
    }
}
