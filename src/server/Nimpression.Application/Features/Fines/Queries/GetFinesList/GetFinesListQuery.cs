using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Fines.DTOs;

namespace Nimpression.Application.Features.Fines.Queries.GetFinesList;

/// <summary>
/// 分页查询交通罚单列表（F8.1 / F8.2）。
/// </summary>
public sealed record GetFinesListQuery(FineFilter Filter) : IRequest<Result<PagedResult<FineDto>>>;
