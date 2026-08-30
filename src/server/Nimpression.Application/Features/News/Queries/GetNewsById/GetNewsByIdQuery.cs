using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.DTOs;

namespace Nimpression.Application.Features.News.Queries.GetNewsById;

/// <summary>
/// 按 ID 获取新闻公告详情 Query。
/// </summary>
public sealed record GetNewsByIdQuery(Guid Id) : IRequest<Result<NewsPostDetailDto>>;
