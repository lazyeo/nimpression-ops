using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Areas.Common;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Application.Features.Notifications.Common;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Application.Features.Notifications.EmailTemplates.Commands.CreateEmailTemplate;

/// <summary>
/// 创建邮件模板命令（F11.2）。
/// 包含中英双语版本与占位符强校验，缺失必要占位符时直接返回 422 业务校验错误。
/// </summary>
public sealed record CreateEmailTemplateCommand(
    string Key,
    string SubjectEn,
    string SubjectZh,
    string BodyEn,
    string BodyZh,
    bool Active = true) : IRequest<Result<Guid>>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "EmailTemplate.Created";
    public string AuditEntityType => "EmailTemplate";
    public Guid? AuditEntityId => null;
}

public sealed class CreateEmailTemplateCommandValidator : AbstractValidator<CreateEmailTemplateCommand>
{
    public CreateEmailTemplateCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage("Template key cannot be empty.")
            .MaximumLength(100)
            .WithMessage("Template key cannot exceed 100 characters.");

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

public sealed class CreateEmailTemplateCommandHandler(
    IEmailTemplateRepository emailTemplateRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateEmailTemplateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        // 1. 占位符业务校验（F11.2: 缺失占位符在保存时校验报错 422）
        var placeholderValidation = TemplatePlaceholderValidator.ValidateRequiredPlaceholders(
            request.Key,
            request.SubjectEn,
            request.SubjectZh,
            request.BodyEn,
            request.BodyZh);

        if (!placeholderValidation.IsSuccess)
        {
            return placeholderValidation.Error!;
        }

        EmailTemplate template;
        try
        {
            template = new EmailTemplate(
                Guid.NewGuid(),
                request.Key,
                request.SubjectEn,
                request.SubjectZh,
                request.BodyEn,
                request.BodyZh,
                request.Active);
        }
        catch (DomainValidationException ex)
        {
            return Error.Validation("invalid_template_data", ex.Message);
        }

        try
        {
            await emailTemplateRepository.AddAsync(template, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
        {
            return Error.Conflict("email_template_conflict", $"Email template with key '{request.Key.Trim().ToUpperInvariant()}' already exists.");
        }

        return template.Id;
    }
}
