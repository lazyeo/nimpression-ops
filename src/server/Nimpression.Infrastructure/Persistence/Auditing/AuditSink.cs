using Nimpression.Application.Common.Abstractions;
using Nimpression.Domain.Entities.Standalone;

namespace Nimpression.Infrastructure.Persistence.Auditing;

/// <summary>
/// 审计日志落地实现。将审计事件追加到 DbContext 中，
/// 与业务变更处于同一事务，交由 UnitOfWork 统一提交。
/// </summary>
public sealed class AuditSink(
    AppDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider? dateTimeProvider = null) : IAuditSink
{
    public Task RecordAsync(
        string entityType,
        Guid? entityId,
        string action,
        string? beforeJson,
        string? afterJson,
        CancellationToken cancellationToken = default)
    {
        var occurredAt = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        var entityIdStr = entityId?.ToString() ?? Guid.Empty.ToString();

        var auditEvent = new AuditEvent(
            id: Guid.NewGuid(),
            action: action,
            entityType: entityType,
            entityId: entityIdStr,
            occurredAt: occurredAt,
            actorUserId: currentUser.UserId,
            actorRole: currentUser.Role,
            beforeJson: beforeJson,
            afterJson: afterJson,
            ipAddress: currentUser.IpAddress,
            userAgent: currentUser.UserAgent);

        dbContext.AuditEvents.Add(auditEvent);
        return Task.CompletedTask;
    }
}
