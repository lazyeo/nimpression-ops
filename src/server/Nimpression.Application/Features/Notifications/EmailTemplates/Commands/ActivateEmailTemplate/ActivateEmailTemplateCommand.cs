using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Application.Features.Notifications.EmailTemplates.Commands.ActivateEmailTemplate;

/// <summary>
/// 启用邮件模板命令（F11.2）。
/// </summary>
public sealed record ActivateEmailTemplateCommand(Guid Id) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "EmailTemplate.Activated";
    public string AuditEntityType => "EmailTemplate";
    public Guid? AuditEntityId => Id;
}

public sealed class ActivateEmailTemplateCommandHandler(
    IEmailTemplateRepository emailTemplateRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ActivateEmailTemplateCommand, Result>
{
    public async Task<Result> Handle(ActivateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await emailTemplateRepository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
        {
            return Error.NotFound("email_template_not_found", $"Email template with ID '{request.Id}' was not found.");
        }

        template.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
