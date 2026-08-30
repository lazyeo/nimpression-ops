using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Application.Features.Notifications.EmailTemplates.Commands.DeactivateEmailTemplate;

/// <summary>
/// 停用邮件模板命令（F11.2）。
/// </summary>
public sealed record DeactivateEmailTemplateCommand(Guid Id) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "EmailTemplate.Deactivated";
    public string AuditEntityType => "EmailTemplate";
    public Guid? AuditEntityId => Id;
}

public sealed class DeactivateEmailTemplateCommandHandler(
    IEmailTemplateRepository emailTemplateRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateEmailTemplateCommand, Result>
{
    public async Task<Result> Handle(DeactivateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await emailTemplateRepository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
        {
            return Error.NotFound("email_template_not_found", $"Email template with ID '{request.Id}' was not found.");
        }

        template.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
