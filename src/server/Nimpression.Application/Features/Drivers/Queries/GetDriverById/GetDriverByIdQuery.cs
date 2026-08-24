using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.DTOs;

namespace Nimpression.Application.Features.Drivers.Queries.GetDriverById;

/// <summary>
/// 按 ID 获取司机详情查询（F2.1）。
/// </summary>
public sealed record GetDriverByIdQuery(Guid DriverId) : IRequest<Result<DriverDetailDto>>;
