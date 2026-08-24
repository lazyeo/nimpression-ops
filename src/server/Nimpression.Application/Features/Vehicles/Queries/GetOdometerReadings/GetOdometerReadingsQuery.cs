using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetOdometerReadings;

/// <summary>
/// 按车辆 ID 获取里程读数历史列表查询。
/// </summary>
public sealed record GetOdometerReadingsQuery(
    Guid VehicleId,
    int Limit = 50) : IRequest<Result<IReadOnlyList<OdometerReadingDto>>>;
