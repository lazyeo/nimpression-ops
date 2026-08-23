using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Standalone;

/// <summary>
/// 发件箱（Outbox）实体。用于领域事件的可靠异步投递与分布式最终一致性。
/// </summary>
public sealed class OutboxMessage : Entity
{
    public string Type { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid id,
        string type,
        string payloadJson,
        DateTimeOffset occurredAt) : base(id)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new DomainValidationException("Outbox message type cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new DomainValidationException("Payload JSON cannot be empty.");
        }

        Type = type.Trim();
        PayloadJson = payloadJson.Trim();
        OccurredAt = occurredAt;
        Attempts = 0;
    }

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
        Error = null;
    }

    public void RecordAttempt(string? error = null)
    {
        Attempts++;
        Error = error;
    }
}
