using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Fines.DTOs;

/// <summary>
/// 交通罚单查询过滤参数。
/// </summary>
public sealed record FineFilter(
    Guid? DriverId = null,
    Guid? VehicleId = null,
    FineStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);
