using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Identity.DTOs;

public sealed record AuditEventDto(
    Guid Id,
    string Action,
    string EntityType,
    string EntityId,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    UserRole? ActorRole,
    string? BeforeJson,
    string? AfterJson,
    string? IpAddress,
    string? UserAgent);

public sealed record AuditExportResult(
    byte[] Bytes,
    string FileName,
    string ContentType);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
