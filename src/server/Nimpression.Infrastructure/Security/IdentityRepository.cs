using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Identity.Abstractions;
using Nimpression.Application.Features.Identity.DTOs;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Security;

public sealed class IdentityRepository(AppDbContext dbContext) : IIdentityRepository
{
    public async Task<User?> GetUserByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task AddRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }

    public async Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<List<RefreshToken>> GetActiveRefreshTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditEventDto>> QueryAuditLogsAsync(
        Guid? actorUserId,
        string? entityType,
        string? entityId,
        string? action,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildAuditQuery(actorUserId, entityType, entityId, action, fromUtc, toUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditEventDto(
                a.Id,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.OccurredAt,
                a.ActorUserId,
                a.ActorRole,
                a.BeforeJson,
                a.AfterJson,
                a.IpAddress,
                a.UserAgent))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEventDto>(items, totalCount, page, pageSize);
    }

    public async Task<List<AuditEventDto>> QueryAllAuditLogsAsync(
        Guid? actorUserId,
        string? entityType,
        string? entityId,
        string? action,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default)
    {
        var query = BuildAuditQuery(actorUserId, entityType, entityId, action, fromUtc, toUtc);

        return await query
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new AuditEventDto(
                a.Id,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.OccurredAt,
                a.ActorUserId,
                a.ActorRole,
                a.BeforeJson,
                a.AfterJson,
                a.IpAddress,
                a.UserAgent))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Domain.Entities.Standalone.AuditEvent> BuildAuditQuery(
        Guid? actorUserId,
        string? entityType,
        string? entityId,
        string? action,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc)
    {
        var query = dbContext.AuditEvents.AsNoTracking().AsQueryable();

        if (actorUserId.HasValue)
        {
            query = query.Where(a => a.ActorUserId == actorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType.Trim());
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(a => a.EntityId == entityId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action.Trim());
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.OccurredAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.OccurredAt <= toUtc.Value);
        }

        return query;
    }
}
