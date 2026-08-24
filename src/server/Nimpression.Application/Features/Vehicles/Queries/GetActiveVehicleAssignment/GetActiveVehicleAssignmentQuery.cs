using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetActiveVehicleAssignment;

/// <summary>
/// 获取指定车辆当前生效中的分派记录查询。若无生效中分派则返回 null。
/// </summary>
public sealed record GetActiveVehicleAssignmentQuery(Guid VehicleId) : IRequest<Result<VehicleAssignmentDto?>>;
