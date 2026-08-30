using MediatR;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.DTOs;

namespace Nimpression.Application.Features.News.Queries.GetNewsReadStats;

/// <summary>
/// 查询新闻公告已读统计 Query（F10.2）。
/// </summary>
public sealed record GetNewsReadStatsQuery(Guid NewsPostId) : IRequest<Result<NewsReadStatsDto>>;
