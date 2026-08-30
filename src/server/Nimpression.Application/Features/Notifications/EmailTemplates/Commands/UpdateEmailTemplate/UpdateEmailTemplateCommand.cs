using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Notifications.EmailTemplates.Commands.UpdateEmailTemplate;

/// <summary>
/// 更新邮件模板内容命令（F11.2）。
/// 包含占位符完整性校验（缺失时返回 422）。
/// </summary>
public sealed record UpdateEmailTemplateCommand(
    Guid Id,
    string SubjectEn,
    string SubjectZh,
    string BodyEn,
    string BodyZh) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "EmailTemplate.Updated";
    public string AuditEntityType => "EmailTemplate";
    public Guid? AuditEntityId => Id;
}

public sealed class UpdateEmailTemplateCommandValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    public UpdateEmailTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Template ID is required.");

        RuleFor(x => x.SubjectEn)
            .NotEmpty()
            .WithMessage("English subject is required.")
            .MaximumLength(255)
            .WithMessage("Subject cannot exceed 255 characters.");

        RuleFor(x => x.SubjectZh)
            .NotEmpty()
            .WithMessage("Chinese subject is required.")
            .MaximumLength(255)
            .WithMessage("Subject cannot exceed 255 characters.");

        RuleFor(x => x.BodyEn)
            .NotEmpty()
            .WithMessage("English body is required.");

        RuleFor(x => x.BodyZh)
            .NotEmpty()
            .WithMessage("Chinese body is required.");
    }
}

public sealed class UpdateEmailTemplateCommandHandler(
    IEmailTemplateRepository emailTemplateRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateEmailTemplateCommand, Result>
{
    public async Task<Result> Handle(UpdateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await emailTemplateRepository.GetByIdAsync(request.Id, cancellationToken);
        if (template is null)
        {
            return Error.NotFound("email_template_not_found", $"Email template with ID '{request.Id}' was not found.");
        }

        // 1. 占位符校验（F11.2: 缺失占位符在保存时校验报错 422）
        var placeholderValidation = TemplatePlaceholderValidator.ValidateRequiredPlaceholders(
            template.Key,
            request.SubjectEn,
            request.SubjectZh,
            request.BodyEn,
            request.BodyZh);

        if (!placeholderValidation.IsSuccess)
        {
            return placeholderValidation.Error!;
        }

        try
        {
            template.UpdateContent(request.SubjectEn, request.SubjectZh, request.BodyEn, request.BodyZh);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_template_data", ex.Message);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
