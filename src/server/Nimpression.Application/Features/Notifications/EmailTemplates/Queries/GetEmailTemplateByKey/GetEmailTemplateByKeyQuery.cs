using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;

namespace Nimpression.Application.Features.Notifications.EmailTemplates.Queries.GetEmailTemplateByKey;

/// <summary>
/// 按 Key 获取邮件模板详情查询（F11.2）。
/// </summary>
public sealed record GetEmailTemplateByKeyQuery(string Key) : IRequest<Result<EmailTemplateDto>>;

public sealed class GetEmailTemplateByKeyQueryHandler(
    IEmailTemplateRepository emailTemplateRepository) : IRequestHandler<GetEmailTemplateByKeyQuery, Result<EmailTemplateDto>>
{
    public async Task<Result<EmailTemplateDto>> Handle(GetEmailTemplateByKeyQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return Error.Validation("invalid_key", "Template key cannot be empty.");
        }

        var template = await emailTemplateRepository.GetByKeyAsync(request.Key, cancellationToken);
        if (template is null)
        {
            return Error.NotFound("email_template_not_found", $"Email template with key '{request.Key}' was not found.");
        }

        return new EmailTemplateDto(
            template.Id,
            template.Key,
            template.SubjectEn,
            template.SubjectZh,
            template.BodyEn,
            template.BodyZh,
            template.Active);
    }
}
