using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Standalone;

/// <summary>
/// 隐私主体权利请求实体（查阅导出 / 匿名化删除 / 更正）。
/// </summary>
public sealed class DataSubjectRequest : AggregateRoot
{
    public Guid SubjectUserId { get; private set; }
    public DataSubjectRequestKind Kind { get; private set; }
    public string Status { get; private set; } = "Pending";
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ExportKey { get; private set; }
    public string? RejectionReason { get; private set; }

    private DataSubjectRequest()
    {
    }

    public DataSubjectRequest(
        Guid id,
        Guid subjectUserId,
        DataSubjectRequestKind kind,
        DateTimeOffset requestedAt) : base(id)
    {
        if (subjectUserId == Guid.Empty)
        {
            throw new DomainValidationException("SubjectUserId cannot be empty.");
        }

        SubjectUserId = subjectUserId;
        Kind = kind;
        RequestedAt = requestedAt;
        Status = "Pending";
    }

    public void Complete(string? exportKey, DateTimeOffset completedAt)
    {
        if (Status != "Pending")
        {
            throw new DomainValidationException($"Cannot complete DSR in '{Status}' status.");
        }

        Status = "Completed";
        ExportKey = string.IsNullOrWhiteSpace(exportKey) ? null : exportKey.Trim();
        CompletedAt = completedAt;
    }

    public void Reject(string reason, DateTimeOffset rejectedAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Rejection reason cannot be empty.");
        }

        if (Status != "Pending")
        {
            throw new DomainValidationException($"Cannot reject DSR in '{Status}' status.");
        }

        Status = "Rejected";
        RejectionReason = reason.Trim();
        CompletedAt = rejectedAt;
    }
}
