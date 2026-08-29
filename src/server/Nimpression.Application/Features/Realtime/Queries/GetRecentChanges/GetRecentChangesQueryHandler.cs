using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Realtime.Queries.GetRecentChanges;

/// <summary>
/// 增量变更拉取处理器。
/// 提供服务端“某时间点之后的变更列表”，用于客户端断线重连后的增量补齐（F12.3）。
/// 严格根据当前用户角色与司机身份实施行级权限隔离（F12.2）。
/// </summary>
public sealed class GetRecentChangesQueryHandler(
    IRealtimeChangesRepository changesRepository,
    IDriverRepository driverRepository,
    ICurrentUser currentUser) : IRequestHandler<GetRecentChangesQuery, Result<List<RealtimeChangeDto>>>
{
    public async Task<Result<List<RealtimeChangeDto>>> Handle(
        GetRecentChangesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return Error.Unauthorized("AUTH_UNAUTHORIZED", "Authentication required to fetch realtime changes.");
        }

        Guid? driverId = null;
        if (currentUser.Role == UserRole.Driver)
        {
            var driver = await driverRepository.GetByUserIdAsync(currentUser.UserId.Value, cancellationToken);
            driverId = driver?.Id;
        }

        var limit = Math.Clamp(request.Limit, 1, 500);
        var changes = await changesRepository.GetChangesSinceAsync(
            request.Since,
            driverId,
            currentUser.Role,
            limit,
            cancellationToken);

        return changes;
    }
}
