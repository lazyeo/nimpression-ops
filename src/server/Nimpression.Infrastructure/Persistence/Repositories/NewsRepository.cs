using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.News.Abstractions;
using Nimpression.Application.Features.News.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Enums;

namespace Nimpression.Infrastructure.Persistence.Repositories;

/// <summary>
/// 新闻公告仓储实现。
/// </summary>
public sealed class NewsRepository(AppDbContext dbContext) : INewsRepository
{
    public async Task<NewsPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.NewsPosts.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task AddNewsPostAsync(NewsPost post, CancellationToken cancellationToken = default)
    {
        await dbContext.NewsPosts.AddAsync(post, cancellationToken);
    }

    public void UpdateNewsPost(NewsPost post)
    {
        dbContext.NewsPosts.Update(post);
    }

    public async Task AddReadReceiptAsync(NewsReadReceipt receipt, CancellationToken cancellationToken = default)
    {
        await dbContext.NewsReadReceipts.AddAsync(receipt, cancellationToken);
    }

    public async Task<PagedResult<NewsPostListItemDto>> GetNewsListAsync(
        NewsListFilter filter,
        Guid? currentUserId,
        UserRole? currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.NewsPosts.AsNoTracking().AsQueryable();

        // 角色受众可见性过滤
        if (currentUserRole == UserRole.Driver)
        {
            query = query.Where(n => n.Audience == NewsAudience.All || n.Audience == NewsAudience.Drivers);
        }
        else if (currentUserRole == UserRole.Dispatcher)
        {
            query = query.Where(n => n.Audience == NewsAudience.All || n.Audience == NewsAudience.Dispatchers);
        }

        if (filter.Audience.HasValue)
        {
            query = query.Where(n => n.Audience == filter.Audience.Value);
        }

        if (filter.IsPinned.HasValue)
        {
            query = query.Where(n => n.Pinned == filter.IsPinned.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(n => n.IsActive == filter.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        // 置顶优先，同置顶状态内按发布时间倒序；列表查询用投影
        var items = await query
            .OrderByDescending(n => n.Pinned)
            .ThenByDescending(n => n.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NewsPostListItemDto(
                n.Id,
                n.Title,
                n.Audience,
                n.PublishedAt,
                n.Pinned,
                n.IsActive,
                currentUserId.HasValue && dbContext.NewsReadReceipts.Any(r => r.NewsPostId == n.Id && r.UserId == currentUserId.Value),
                currentUserId.HasValue
                    ? dbContext.NewsReadReceipts.Where(r => r.NewsPostId == n.Id && r.UserId == currentUserId.Value).Select(r => (DateTimeOffset?)r.ReadAt).FirstOrDefault()
                    : null))
            .ToListAsync(cancellationToken);

        return new PagedResult<NewsPostListItemDto>(items, totalCount, page, pageSize);
    }

    public async Task<NewsPostDetailDto?> GetNewsDetailAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.NewsPosts.AsNoTracking()
            .Where(n => n.Id == id)
            .Join(dbContext.Users.AsNoTracking(), n => n.AuthorUserId, u => u.Id, (n, u) => new { n, u })
            .Select(x => new NewsPostDetailDto(
                x.n.Id,
                x.n.AuthorUserId,
                x.u.DisplayName,
                x.n.Title,
                x.n.BodyEn,
                x.n.BodyZh,
                x.n.Audience,
                x.n.PublishedAt,
                x.n.Pinned,
                x.n.IsActive,
                currentUserId.HasValue && dbContext.NewsReadReceipts.Any(r => r.NewsPostId == x.n.Id && r.UserId == currentUserId.Value),
                currentUserId.HasValue
                    ? dbContext.NewsReadReceipts.Where(r => r.NewsPostId == x.n.Id && r.UserId == currentUserId.Value).Select(r => (DateTimeOffset?)r.ReadAt).FirstOrDefault()
                    : null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NewsReadStatsDto?> GetReadStatsAsync(
        Guid newsPostId,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.NewsPosts.AsNoTracking()
            .Where(n => n.Id == newsPostId)
            .Select(n => new { n.Id, n.Audience })
            .FirstOrDefaultAsync(cancellationToken);

        if (post == null)
        {
            return null;
        }

        // 「已读 7/10」的分母是该新闻受众范围内的人数，且已停用的账号不计入分母
        var audienceUsers = dbContext.Users.AsNoTracking()
            .Where(u => u.Status == UserStatus.Active);

        if (post.Audience == NewsAudience.Drivers)
        {
            audienceUsers = audienceUsers.Where(u => u.Role == UserRole.Driver);
        }
        else if (post.Audience == NewsAudience.Dispatchers)
        {
            audienceUsers = audienceUsers.Where(u => u.Role == UserRole.Dispatcher);
        }

        var totalAudience = await audienceUsers.CountAsync(cancellationToken);

        // 分子：受众范围内且已读的人数
        var readCount = await dbContext.NewsReadReceipts.AsNoTracking()
            .Where(r => r.NewsPostId == newsPostId && audienceUsers.Any(u => u.Id == r.UserId))
            .CountAsync(cancellationToken);

        var rate = totalAudience > 0 ? Math.Round((double)readCount / totalAudience, 4) : 0.0;

        return new NewsReadStatsDto(newsPostId, readCount, totalAudience, rate);
    }

    public async Task<IReadOnlyList<UnreadUserDto>> GetUnreadUsersAsync(
        Guid newsPostId,
        CancellationToken cancellationToken = default)
    {
        var post = await dbContext.NewsPosts.AsNoTracking()
            .Where(n => n.Id == newsPostId)
            .Select(n => new { n.Id, n.Audience })
            .FirstOrDefaultAsync(cancellationToken);

        if (post == null)
        {
            return Array.Empty<UnreadUserDto>();
        }

        // 目标受众中处于激活状态的用户（排除停用账号）
        var audienceUsers = dbContext.Users.AsNoTracking()
            .Where(u => u.Status == UserStatus.Active);

        if (post.Audience == NewsAudience.Drivers)
        {
            audienceUsers = audienceUsers.Where(u => u.Role == UserRole.Driver);
        }
        else if (post.Audience == NewsAudience.Dispatchers)
        {
            audienceUsers = audienceUsers.Where(u => u.Role == UserRole.Dispatcher);
        }

        var readUserIds = dbContext.NewsReadReceipts.AsNoTracking()
            .Where(r => r.NewsPostId == newsPostId)
            .Select(r => r.UserId);

        var unreadList = await audienceUsers
            .Where(u => !readUserIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .GroupJoin(
                dbContext.Drivers.AsNoTracking(),
                u => u.Id,
                d => d.UserId,
                (u, drivers) => new { u, driver = drivers.FirstOrDefault() })
            .Select(x => new UnreadUserDto(
                x.u.Id,
                x.u.DisplayName,
                x.u.Email.Value,
                x.u.Role,
                x.driver != null ? x.driver.EmployeeNo : null))
            .ToListAsync(cancellationToken);

        return unreadList;
    }
}
