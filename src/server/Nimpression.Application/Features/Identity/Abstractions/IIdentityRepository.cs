using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Identity.Abstractions;

public interface IIdentityRepository
{
    Task<User?> GetUserByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);

    Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<List<RefreshToken>> GetActiveRefreshTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PagedResult<AuditEventDto>> QueryAuditLogsAsync(
        Guid? actorUserId,
        string? entityType,
        string? entityId,
        string? action,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<List<AuditEventDto>> QueryAllAuditLogsAsync(
        Guid? actorUserId,
        string? entityType,
        string? entityId,
        string? action,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default);
}
