using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Enums;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Realtime.Services;

namespace Nimpression.Infrastructure.Realtime.Persistence;

/// <summary>
/// 增量变更仓储实现。从持久化的 Outbox 发件箱中拉取指定时间之后的失效变更，
/// 严格执行角色与司机维度的数据隔离。
/// </summary>
public sealed class RealtimeChangesRepository(
    AppDbContext dbContext,
    IOutboxToRealtimeMapper mapper) : IRealtimeChangesRepository
{
    public async Task<List<RealtimeChangeDto>> GetChangesSinceAsync(
        DateTimeOffset since,
        Guid? driverId,
        UserRole? role,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        // 限制单次最大拉取行数，避免大量数据加载
        var fetchCount = Math.Clamp(limit * 2, 50, 1000);

        var outboxMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(m => m.OccurredAt >= since)
            .OrderBy(m => m.OccurredAt)
            .Take(fetchCount)
            .ToListAsync(cancellationToken);

        var results = new List<RealtimeChangeDto>(limit);

        foreach (var outboxMessage in outboxMessages)
        {
            var mapping = mapper.Map(outboxMessage);

            var isAllowed = IsAuthorizedForChange(mapping, driverId, role);
            if (isAllowed)
            {
                results.Add(new RealtimeChangeDto(
                    mapping.Message.Kind,
                    mapping.Message.EntityId,
                    mapping.Message.OccurredAt));

                if (results.Count >= limit)
                {
                    break;
                }
            }
        }

        return results;
    }

    private static bool IsAuthorizedForChange(
        OutboxRealtimeMapping mapping,
        Guid? driverId,
        UserRole? role)
    {
        if (role == UserRole.Admin)
        {
            return true;
        }

        if (role == UserRole.Dispatcher)
        {
            // 调度员可接收公共广播、调度员通知及全体司机通知
            return mapping.TargetGroups.Contains(RealtimeGroupNames.All)
                || mapping.TargetGroups.Contains(RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()))
                || mapping.TargetGroups.Contains(RealtimeGroupNames.Role(UserRole.Admin.ToString()));
        }

        if (role == UserRole.Driver)
        {
            // 司机仅能获取广播通知、全体司机通知或本人的私有通知
            if (mapping.TargetGroups.Contains(RealtimeGroupNames.All)
                || mapping.TargetGroups.Contains(RealtimeGroupNames.Role(UserRole.Driver.ToString())))
            {
                return true;
            }

            if (driverId.HasValue && driverId.Value != Guid.Empty)
            {
                if (mapping.TargetDriverId == driverId.Value
                    || mapping.TargetGroups.Contains(RealtimeGroupNames.Driver(driverId.Value)))
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }
}
