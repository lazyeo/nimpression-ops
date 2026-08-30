using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.DTOs;

namespace Nimpression.Application.Features.Notifications.EmailTemplates.Queries.GetEmailTemplateById;

/// <summary>
/// 按 ID 获取邮件模板详情查询（F11.2）。
/// </summary>
public sealed record GetEmailTemplateByIdQuery(Guid Id) : IRequest<Result<EmailTemplateDto>>;

public sealed class GetEmailTemplateByIdQueryHandler(
    IEmailTemplateRepository emailTemplateRepository) : IRequestHandler<GetEmailTemplateByIdQuery, Result<EmailTemplateDto>>
{
    public async Task<Result<EmailTemplateDto>> Handle(GetEmailTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var template = await emailTemplateRepository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
        {
            return Error.NotFound("email_template_not_found", $"Email template with ID '{request.Id}' was not found.");
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
