using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Abstractions;
using Nimpression.Application.Features.News.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.Queries.GetNewsReadStats;

/// <summary>
/// 查询新闻公告已读统计 Handler（F10.2）。
/// </summary>
public sealed class GetNewsReadStatsQueryHandler(
    INewsRepository newsRepository,
    ICurrentUser currentUser) : IRequestHandler<GetNewsReadStatsQuery, Result<NewsReadStatsDto>>
{
    public async Task<Result<NewsReadStatsDto>> Handle(GetNewsReadStatsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Dispatcher)
        {
            return Error.Forbidden("forbidden", "Only managers and dispatchers can view read stats.");
        }

        var post = await newsRepository.GetByIdAsync(request.NewsPostId, cancellationToken);
        if (post == null)
        {
            return Error.NotFound("news_not_found", "News post was not found.");
        }

        var stats = await newsRepository.GetReadStatsAsync(request.NewsPostId, cancellationToken);
        if (stats == null)
        {
            return Error.NotFound("news_not_found", "News post was not found.");
        }

        return stats;
    }
}
