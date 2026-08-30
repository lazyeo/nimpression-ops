using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.DTOs;

namespace Nimpression.Application.Features.News.Queries.GetNewsList;

/// <summary>
/// 查询新闻公告分页列表 Query。
/// </summary>
public sealed record GetNewsListQuery(NewsListFilter Filter) : IRequest<Result<PagedResult<NewsPostListItemDto>>>;
