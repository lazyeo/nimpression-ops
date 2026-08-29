using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Dispatch.DTOs;

/// <summary>
/// 派发任务过滤参数。
/// </summary>
public sealed record JobTaskFilter(
    Guid? DriverId = null,
    Guid? VehicleId = null,
    Guid? AreaId = null,
    JobTaskStatus? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);
