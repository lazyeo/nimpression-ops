using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Dispatch.Queries.GetMyJobTasks;

/// <summary>
/// 司机端查询本人名下派发任务列表查询。
/// 严格从 JWT 提取当前用户身份并关联 Drivers 记录，绝不信任客户端传入的 driverId。
/// </summary>
public sealed record GetMyJobTasksQuery(JobTaskStatus? Status = null) : IRequest<Result<List<DriverTaskItemDto>>>;

public sealed class GetMyJobTasksQueryHandler(
    IJobTaskRepository jobTaskRepository,
    ICurrentUser currentUser) : IRequestHandler<GetMyJobTasksQuery, Result<List<DriverTaskItemDto>>>
{
    public async Task<Result<List<DriverTaskItemDto>>> Handle(GetMyJobTasksQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Driver || !currentUser.UserId.HasValue)
        {
            return Error.Forbidden("forbidden", "Only drivers can access their tasks.");
        }

        var driverId = await jobTaskRepository.GetDriverIdByUserIdAsync(currentUser.UserId.Value, cancellationToken);
        if (!driverId.HasValue)
        {
            return Error.Forbidden("driver_profile_not_found", "Current user is not associated with a driver profile.");
        }

        var tasks = await jobTaskRepository.GetDriverTasksAsync(driverId.Value, request.Status, cancellationToken);
        return Result<List<DriverTaskItemDto>>.Success(tasks);
    }
}
