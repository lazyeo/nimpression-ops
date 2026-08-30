using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Communications;

namespace Nimpression.Application.Features.Notifications.Abstractions;

/// <summary>
/// 邮件模板仓储接口（F11.2）。
/// </summary>
public interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmailTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<PagedResult<EmailTemplateDto>> GetListAsync(EmailTemplateFilter filter, CancellationToken cancellationToken = default);

    Task AddAsync(EmailTemplate template, CancellationToken cancellationToken = default);
}
