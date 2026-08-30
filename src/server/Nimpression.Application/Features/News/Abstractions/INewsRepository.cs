using Nimpression.Application.Features.News.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.Abstractions;

/// <summary>
/// 新闻公告仓储接口。
/// </summary>
public interface INewsRepository
{
    Task<NewsPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddNewsPostAsync(NewsPost post, CancellationToken cancellationToken = default);

    void UpdateNewsPost(NewsPost post);

    Task AddReadReceiptAsync(NewsReadReceipt receipt, CancellationToken cancellationToken = default);

    Task<PagedResult<NewsPostListItemDto>> GetNewsListAsync(
        NewsListFilter filter,
        Guid? currentUserId,
        UserRole? currentUserRole,
        CancellationToken cancellationToken = default);

    Task<NewsPostDetailDto?> GetNewsDetailAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    Task<NewsReadStatsDto?> GetReadStatsAsync(
        Guid newsPostId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnreadUserDto>> GetUnreadUsersAsync(
        Guid newsPostId,
        CancellationToken cancellationToken = default);
}
