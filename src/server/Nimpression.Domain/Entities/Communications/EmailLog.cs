using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Communications;

/// <summary>
/// 邮件发送日志聚合根。跟踪投递状态、重试次数与去重 CorrelationId。
/// </summary>
public sealed class EmailLog : AggregateRoot
{
    public string TemplateKey { get; private set; } = string.Empty;
    public EmailAddress ToAddress { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Pending";
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string TriggeredBy { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;

    private EmailLog()
    {
    }

    public EmailLog(
        Guid id,
        string templateKey,
        EmailAddress toAddress,
        string subject,
        string triggeredBy,
        string correlationId) : base(id)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            throw new DomainValidationException("Template key cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new DomainValidationException("Subject cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(triggeredBy))
        {
            throw new DomainValidationException("TriggeredBy cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new DomainValidationException("CorrelationId cannot be empty.");
        }

        TemplateKey = templateKey.Trim().ToUpperInvariant();
        ToAddress = toAddress;
        Subject = subject.Trim();
        TriggeredBy = triggeredBy.Trim();
        CorrelationId = correlationId.Trim();
        Status = "Pending";
        Attempts = 0;
    }

    public void RecordSuccess(DateTimeOffset sentAt)
    {
        Status = "Sent";
        SentAt = sentAt;
        Attempts++;
    }

    public void RecordFailure(string error)
    {
        Status = "Failed";
        LastError = error;
        Attempts++;
    }
}
