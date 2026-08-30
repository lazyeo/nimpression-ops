using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;

namespace Nimpression.Application.Features.Notifications.EmailLogs.Queries.GetEmailLogById;

/// <summary>
/// 按 ID 获取邮件发送日志详情查询（F11.5）。
/// </summary>
public sealed record GetEmailLogByIdQuery(Guid Id) : IRequest<Result<EmailLogDto>>;

public sealed class GetEmailLogByIdQueryHandler(
    IEmailLogRepository emailLogRepository) : IRequestHandler<GetEmailLogByIdQuery, Result<EmailLogDto>>
{
    public async Task<Result<EmailLogDto>> Handle(GetEmailLogByIdQuery request, CancellationToken cancellationToken)
    {
        var log = await emailLogRepository.GetByIdAsync(request.Id, cancellationToken);
        if (log is null)
        {
            return Error.NotFound("email_log_not_found", $"Email log with ID '{request.Id}' was not found.");
        }

        return new EmailLogDto(
            log.Id,
            log.TemplateKey,
            log.ToAddress.Value,
            log.Subject,
            log.Status,
            log.Attempts,
            log.LastError,
            log.SentAt,
            log.TriggeredBy,
            log.CorrelationId);
    }
}
