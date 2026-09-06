using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Dispatch.Queries.GetMyJobTasks;

/// <summary>
/// 司机端查询本人名下派发任务列表查询（支持活跃/历史状态划分与服务端分页）。
/// 严格从 JWT 提取当前用户身份并关联 Drivers 记录，绝不信任客户端传入的 driverId。
/// </summary>
/// <param name="Status">可选的任务生命周期状态精确筛选。</param>
/// <param name="ActiveOnly">
/// 活跃状态划分语义（取值互不重叠且并集覆盖全部 6 个 JobTaskStatus）：
/// - true: 仅返回处于活跃状态的任务（Assigned, Acknowledged, InProgress）。
/// - false: 仅返回处于非活跃/历史状态的任务（Draft, Completed, Cancelled）。
/// - null: 不限制活跃状态，返回所有状态任务（全集）。
/// </param>
/// <param name="Page">当前页码（从 1 开始，默认 1）。</param>
/// <param name="PageSize">每页记录数（默认 20，最小 1，最大 100）。</param>
public sealed record GetMyJobTasksQuery(
    JobTaskStatus? Status = null,
    bool? ActiveOnly = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<DriverTaskItemDto>>>;

public sealed class GetMyJobTasksQueryHandler(
    IJobTaskRepository jobTaskRepository,
    ICurrentUser currentUser) : IRequestHandler<GetMyJobTasksQuery, Result<PagedResult<DriverTaskItemDto>>>
{
    public async Task<Result<PagedResult<DriverTaskItemDto>>> Handle(GetMyJobTasksQuery request, CancellationToken cancellationToken)
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

        var tasks = await jobTaskRepository.GetDriverTasksAsync(
            driverId.Value,
            request.Status,
            request.ActiveOnly,
            request.Page,
            request.PageSize,
            cancellationToken);

        return Result<PagedResult<DriverTaskItemDto>>.Success(tasks);
    }
}
