using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Dispatch.Abstractions;
using Nimpression.Application.Features.Dispatch.DTOs;

namespace Nimpression.Application.Features.Dispatch.Queries.GetUnacknowledgedTaskAlerts;

/// <summary>
/// 未确认派发任务预警查询（F5.5）。
/// 指派后超过阈值（默认 30 分钟）仍处于 Assigned 状态未被确认的任务产出提醒。
/// </summary>
public sealed record GetUnacknowledgedTaskAlertsQuery(int ThresholdMinutes = 30) : IRequest<Result<List<JobTaskAlertDto>>>;

public sealed class GetUnacknowledgedTaskAlertsQueryHandler(
    IJobTaskRepository jobTaskRepository,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetUnacknowledgedTaskAlertsQuery, Result<List<JobTaskAlertDto>>>
{
    public async Task<Result<List<JobTaskAlertDto>>> Handle(GetUnacknowledgedTaskAlertsQuery request, CancellationToken cancellationToken)
    {
        var threshold = Math.Max(1, request.ThresholdMinutes);
        var alerts = await jobTaskRepository.GetUnacknowledgedTaskAlertsAsync(
            threshold,
            dateTimeProvider.UtcNow,
            cancellationToken);

        return alerts;
    }
}
