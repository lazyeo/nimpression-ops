using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.DTOs;

namespace Nimpression.Application.Features.Timesheets.Queries.GetCurrentActiveShift;

/// <summary>
/// 获取指定司机或当前登录司机的进行中活跃班次。
/// </summary>
public sealed record GetCurrentActiveShiftQuery(Guid? DriverId = null) : IRequest<Result<ShiftEntryDto?>>;
