using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetVehicleAssignments;

/// <summary>
/// 按车辆 ID 获取分派历史记录列表查询。
/// </summary>
public sealed record GetVehicleAssignmentsQuery(Guid VehicleId) : IRequest<Result<IReadOnlyList<VehicleAssignmentDto>>>;
