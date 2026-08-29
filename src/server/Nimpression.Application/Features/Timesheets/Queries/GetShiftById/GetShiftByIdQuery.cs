using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.DTOs;

namespace Nimpression.Application.Features.Timesheets.Queries.GetShiftById;

/// <summary>
/// 按 ID 获取班次打卡记录详情。
/// </summary>
public sealed record GetShiftByIdQuery(Guid ShiftId) : IRequest<Result<ShiftEntryDto>>;
