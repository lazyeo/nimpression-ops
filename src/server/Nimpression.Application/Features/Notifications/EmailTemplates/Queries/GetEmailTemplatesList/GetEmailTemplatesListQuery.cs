using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;

namespace Nimpression.Application.Features.Notifications.EmailTemplates.Queries.GetEmailTemplatesList;

/// <summary>
/// 获取邮件模板列表查询（F11.2）。
/// </summary>
public sealed record GetEmailTemplatesListQuery(EmailTemplateFilter Filter) : IRequest<Result<PagedResult<EmailTemplateDto>>>;

public sealed class GetEmailTemplatesListQueryHandler(
    IEmailTemplateRepository emailTemplateRepository) : IRequestHandler<GetEmailTemplatesListQuery, Result<PagedResult<EmailTemplateDto>>>
{
    public async Task<Result<PagedResult<EmailTemplateDto>>> Handle(GetEmailTemplatesListQuery request, CancellationToken cancellationToken)
    {
        var result = await emailTemplateRepository.GetListAsync(request.Filter, cancellationToken);
        return result;
    }
}
