using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Abstractions;
using Nimpression.Application.Features.News.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.Queries.GetNewsById;

/// <summary>
/// 按 ID 获取新闻公告详情 Handler。
/// 遵循权限控制规则（非受众越权访问返回 403 而非 404）。
/// </summary>
public sealed class GetNewsByIdQueryHandler(
    INewsRepository newsRepository,
    ICurrentUser currentUser) : IRequestHandler<GetNewsByIdQuery, Result<NewsPostDetailDto>>
{
    public async Task<Result<NewsPostDetailDto>> Handle(GetNewsByIdQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        var post = await newsRepository.GetNewsDetailAsync(request.Id, currentUser.UserId, cancellationToken);
        if (post == null)
        {
            return Error.NotFound("news_not_found", "News post was not found.");
        }

        // 越权检查：司机无权查看调度员专有公告（返回 403）
        if (currentUser.Role == UserRole.Driver && post.Audience == NewsAudience.Dispatchers)
        {
            return Error.Forbidden("forbidden", "Drivers cannot view dispatcher-specific news.");
        }

        return post;
    }
}
