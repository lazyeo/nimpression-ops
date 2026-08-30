using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Application.Features.Notifications.EmailLogs.Commands.ResendEmail;

/// <summary>
/// 手动重发失败邮件命令（F11.5）。
/// </summary>
public sealed record ResendEmailCommand(Guid Id) : IRequest<Result>, ICommandMarker, IAuditableCommand
{
    public string AuditAction => "EmailLog.Resent";
    public string AuditEntityType => "EmailLog";
    public Guid? AuditEntityId => Id;
}

public sealed class ResendEmailCommandHandler(
    IEmailLogRepository emailLogRepository,
    IEmailTemplateRepository emailTemplateRepository,
    IEmailSender emailSender,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : IRequestHandler<ResendEmailCommand, Result>
{
    public async Task<Result> Handle(ResendEmailCommand request, CancellationToken cancellationToken)
    {
        var log = await emailLogRepository.GetByIdAsync(request.Id, cancellationToken);
        if (log is null)
        {
            return Error.NotFound("email_log_not_found", $"Email log with ID '{request.Id}' was not found.");
        }

        if (string.Equals(log.Status, "Sent", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Unprocessable("email_already_sent", "Cannot resend an email that has already been delivered successfully.");
        }

        // 尝试解析模板正文
        var template = await emailTemplateRepository.GetByKeyAsync(log.TemplateKey, cancellationToken);
        var body = template is not null
            ? $"{template.BodyEn}\n\n{template.BodyZh}"
            : log.Subject;

        try
        {
            await emailSender.SendEmailAsync(log.ToAddress.Value, log.Subject, body, cancellationToken);
            log.RecordSuccess(dateTimeProvider.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            // 严禁静默吞掉异常（_COMMON.md）：记录失败并返回错误，供管理端感知与后续重试
            log.RecordFailure(ex.Message);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Error.Validation("email_send_failed", $"Failed to send email: {ex.Message}");
        }
    }
}
