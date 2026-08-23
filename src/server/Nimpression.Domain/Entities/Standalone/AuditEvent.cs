using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Standalone;

/// <summary>
/// 审计日志实体。只增不改不删（Append-Only），记录所有写操作的 Before/After 快照与上下文。
/// </summary>
public sealed class AuditEvent : Entity
{
    public Guid? ActorUserId { get; private set; }
    public UserRole? ActorRole { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid id,
        string action,
        string entityType,
        string entityId,
        DateTimeOffset occurredAt,
        Guid? actorUserId = null,
        UserRole? actorRole = null,
        string? beforeJson = null,
        string? afterJson = null,
        string? ipAddress = null,
        string? userAgent = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainValidationException("Audit action cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainValidationException("EntityType cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new DomainValidationException("EntityId cannot be empty.");
        }

        Action = action.Trim();
        EntityType = entityType.Trim();
        EntityId = entityId.Trim();
        OccurredAt = occurredAt;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
}
