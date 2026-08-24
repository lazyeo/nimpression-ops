using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.DTOs;

namespace Nimpression.Application.Features.Vehicles.Queries.GetOdometerReadings;

/// <summary>
/// 车辆里程读数历史查询处理器。
/// </summary>
public sealed class GetOdometerReadingsQueryHandler(
    IVehicleRepository vehicleRepository) : IRequestHandler<GetOdometerReadingsQuery, Result<IReadOnlyList<OdometerReadingDto>>>
{
    public async Task<Result<IReadOnlyList<OdometerReadingDto>>> Handle(GetOdometerReadingsQuery request, CancellationToken cancellationToken)
    {
        var readings = await vehicleRepository.GetOdometerReadingsByVehicleIdAsync(request.VehicleId, request.Limit, cancellationToken);
        return Result<IReadOnlyList<OdometerReadingDto>>.Success(readings);
    }
}
