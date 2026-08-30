using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Abstractions;
using Nimpression.Application.Features.News.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.Queries.GetNewsUnreadUsers;

/// <summary>
/// 查询新闻公告未读人员名单 Handler（F10.2）。
/// </summary>
public sealed class GetNewsUnreadUsersQueryHandler(
    INewsRepository newsRepository,
    ICurrentUser currentUser) : IRequestHandler<GetNewsUnreadUsersQuery, Result<IReadOnlyList<UnreadUserDto>>>
{
    public async Task<Result<IReadOnlyList<UnreadUserDto>>> Handle(GetNewsUnreadUsersQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Error.Unauthorized("unauthorized", "User is not authenticated.");
        }

        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Dispatcher)
        {
            return Error.Forbidden("forbidden", "Only managers and dispatchers can view unread users.");
        }

        var post = await newsRepository.GetByIdAsync(request.NewsPostId, cancellationToken);
        if (post == null)
        {
            return Error.NotFound("news_not_found", "News post was not found.");
        }

        var unreadUsers = await newsRepository.GetUnreadUsersAsync(request.NewsPostId, cancellationToken);
        return Result<IReadOnlyList<UnreadUserDto>>.Success(unreadUsers);
    }
}
