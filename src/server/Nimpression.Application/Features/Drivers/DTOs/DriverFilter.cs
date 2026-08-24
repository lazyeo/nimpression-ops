using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Drivers.DTOs;

/// <summary>
/// 司机列表筛选条件。
/// </summary>
public sealed record DriverFilter(
    string? SearchTerm = null,
    string? Name = null,
    string? EmployeeNo = null,
    DriverStatus? Status = null,
    Guid? AreaId = null,
    int Page = 1,
    int PageSize = 20);
