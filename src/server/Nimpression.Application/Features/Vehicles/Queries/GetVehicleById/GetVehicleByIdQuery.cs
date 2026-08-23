using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehicleById;

/// <summary>
/// 按 ID 获取车辆详情查询。
/// </summary>
public sealed record GetVehicleByIdQuery(Guid Id) : IRequest<Result<VehicleDetailDto>>;
