using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Abstractions;
using Nimpression.Application.Features.News.DTOs;

namespace Nimpression.Application.Features.News.Queries.GetNewsList;

/// <summary>
/// 查询新闻公告分页列表 Handler。
/// </summary>
public sealed class GetNewsListQueryHandler(
    INewsRepository newsRepository,
    ICurrentUser currentUser) : IRequestHandler<GetNewsListQuery, Result<PagedResult<NewsPostListItemDto>>>
{
    public async Task<Result<PagedResult<NewsPostListItemDto>>> Handle(GetNewsListQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        var result = await newsRepository.GetNewsListAsync(
            request.Filter,
            currentUser.UserId,
            currentUser.Role,
            cancellationToken);

        return result;
    }
}
