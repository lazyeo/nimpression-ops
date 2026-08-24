using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Realtime.Abstractions;

/// <summary>
/// 实时增量变更查询仓储契约。
/// 从底层 Outbox 消息记录中提取并过滤指定时间点之后产生的失效变更。
/// </summary>
public interface IRealtimeChangesRepository
{
    /// <summary>
    /// 获取指定时间点之后的增量失效变更列表。
    /// 严格根据调用者角色及司机 ID 实施行级数据权限过滤。
    /// </summary>
    Task<List<RealtimeChangeDto>> GetChangesSinceAsync(
        DateTimeOffset since,
        Guid? driverId,
        UserRole? role,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
