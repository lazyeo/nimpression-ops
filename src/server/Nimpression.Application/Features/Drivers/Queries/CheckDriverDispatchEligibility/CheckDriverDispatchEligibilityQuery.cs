using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.DTOs;

namespace Nimpression.Application.Features.Drivers.Queries.CheckDriverDispatchEligibility;

/// <summary>
/// 检查司机派单资格查询（F2.3）。
/// 校验司机是否处于激活状态且驾照未过期；若到期则返回 422 及明确原因。
/// </summary>
public sealed record CheckDriverDispatchEligibilityQuery(
    Guid DriverId,
    DateOnly? ReferenceDate = null) : IRequest<Result<DriverDispatchEligibilityDto>>;
