using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Fines.DTOs;

namespace Nimpression.Application.Features.Fines.Queries.GetFineById;

/// <summary>
/// 按 ID 获取交通罚单详情查询（F8.1 / F8.4）。
/// </summary>
public sealed record GetFineByIdQuery(Guid FineId) : IRequest<Result<FineDetailDto>>;
