using Nimpression.Application.Features.Notifications.DTOs;
using Nimpression.Domain.Entities.Communications;

namespace Nimpression.Application.Features.Notifications.Abstractions;

/// <summary>
/// 邮件发送日志仓储接口（F11.5）。
/// </summary>
public interface IEmailLogRepository
{
    Task<EmailLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EmailLog?> GetByCorrelationIdAndRecipientAsync(string correlationId, string toAddress, CancellationToken cancellationToken = default);

    Task<PagedResult<EmailLogDto>> GetListAsync(EmailLogFilter filter, CancellationToken cancellationToken = default);

    Task<List<EmailLog>> GetRetryableLogsAsync(int maxAttempts, CancellationToken cancellationToken = default);

    Task AddAsync(EmailLog emailLog, CancellationToken cancellationToken = default);
}
