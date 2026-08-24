using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.DTOs;

namespace Nimpression.Application.Features.Drivers.Queries.GetLicenceAlerts;

/// <summary>
/// 获取驾照到期预警列表查询（F2.3）。
/// 默认查询 30 天内即将到期或已到期的活跃司机。
/// </summary>
public sealed record GetLicenceAlertsQuery(int DaysThreshold = 30) : IRequest<Result<List<DriverLicenceAlertDto>>>;
