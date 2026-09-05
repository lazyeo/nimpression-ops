using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;

namespace Nimpression.Application.Features.Dispatch.Queries.GetDispatchMetrics;

/// <summary>
/// 调度控制台聚合指标看板查询。
/// </summary>
public sealed record GetDispatchMetricsQuery : IRequest<Result<DashboardMetricsDto>>;

public sealed class GetDispatchMetricsQueryHandler(
    IJobTaskRepository jobTaskRepository) : IRequestHandler<GetDispatchMetricsQuery, Result<DashboardMetricsDto>>
{
    public async Task<Result<DashboardMetricsDto>> Handle(GetDispatchMetricsQuery request, CancellationToken cancellationToken)
    {
        var metrics = await jobTaskRepository.GetDashboardMetricsAsync(cancellationToken);
        return Result<DashboardMetricsDto>.Success(metrics);
    }
}
