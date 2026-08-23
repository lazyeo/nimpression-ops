using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Vehicles.DTOs;

/// <summary>
/// 车辆列表筛选条件。
/// </summary>
public sealed record VehicleFilter(
    string? Search = null,
    VehicleStatus? Status = null,
    bool? ServiceDueOnly = null,
    int Page = 1,
    int PageSize = 20);
