using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;

namespace Nimpression.Application.Features.Notifications.EmailLogs.Queries.GetEmailLogsList;

/// <summary>
/// 获取邮件发送日志列表查询（F11.5）。
/// </summary>
public sealed record GetEmailLogsListQuery(EmailLogFilter Filter) : IRequest<Result<PagedResult<EmailLogDto>>>;

public sealed class GetEmailLogsListQueryHandler(
    IEmailLogRepository emailLogRepository) : IRequestHandler<GetEmailLogsListQuery, Result<PagedResult<EmailLogDto>>>
{
    public async Task<Result<PagedResult<EmailLogDto>>> Handle(GetEmailLogsListQuery request, CancellationToken cancellationToken)
    {
        var result = await emailLogRepository.GetListAsync(request.Filter, cancellationToken);
        return result;
    }
}
