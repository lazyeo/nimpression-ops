using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.News.Abstractions;
using Nimpression.Application.Features.News.DTOs;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Tests.News.TestDoubles;

public sealed class FakeNewsRepository : INewsRepository
{
    public Dictionary<Guid, NewsPost> Posts { get; } = [];
    public List<NewsReadReceipt> ReadReceipts { get; } = [];
    public Dictionary<Guid, (User User, string? EmployeeNo)> Users { get; } = [];

    public Task<NewsPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Posts.TryGetValue(id, out var post);
        return Task.FromResult(post);
    }

    public Task AddNewsPostAsync(NewsPost post, CancellationToken cancellationToken = default)
    {
        Posts[post.Id] = post;
        return Task.CompletedTask;
    }

    public void UpdateNewsPost(NewsPost post)
    {
        Posts[post.Id] = post;
    }

    public Task AddReadReceiptAsync(NewsReadReceipt receipt, CancellationToken cancellationToken = default)
    {
        ReadReceipts.Add(receipt);
        return Task.CompletedTask;
    }

    public Task<PagedResult<NewsPostListItemDto>> GetNewsListAsync(
        NewsListFilter filter,
        Guid? currentUserId,
        UserRole? currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var query = Posts.Values.AsEnumerable();

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

        var total = query.Count();
        var items = query
            .OrderByDescending(n => n.Pinned)
            .ThenByDescending(n => n.PublishedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(n => new NewsPostListItemDto(
                n.Id,
                n.Title,
                n.Audience,
                n.PublishedAt,
                n.Pinned,
                n.IsActive,
                currentUserId.HasValue && ReadReceipts.Any(r => r.NewsPostId == n.Id && r.UserId == currentUserId.Value),
                currentUserId.HasValue
                    ? ReadReceipts.Where(r => r.NewsPostId == n.Id && r.UserId == currentUserId.Value).Select(r => (DateTimeOffset?)r.ReadAt).FirstOrDefault()
                    : null))
            .ToList();

        return Task.FromResult(new PagedResult<NewsPostListItemDto>(items, total, filter.Page, filter.PageSize));
    }

    public Task<NewsPostDetailDto?> GetNewsDetailAsync(
        Guid id,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Posts.TryGetValue(id, out var post))
        {
            return Task.FromResult<NewsPostDetailDto?>(null);
        }

        var authorName = Users.TryGetValue(post.AuthorUserId, out var u) ? u.User.DisplayName : "Admin";
        var isRead = currentUserId.HasValue && ReadReceipts.Any(r => r.NewsPostId == id && r.UserId == currentUserId.Value);
        var readAt = currentUserId.HasValue
            ? ReadReceipts.Where(r => r.NewsPostId == id && r.UserId == currentUserId.Value).Select(r => (DateTimeOffset?)r.ReadAt).FirstOrDefault()
            : null;

        var dto = new NewsPostDetailDto(
            post.Id,
            post.AuthorUserId,
            authorName,
            post.Title,
            post.BodyEn,
            post.BodyZh,
            post.Audience,
            post.PublishedAt,
            post.Pinned,
            post.IsActive,
            isRead,
            readAt);

        return Task.FromResult<NewsPostDetailDto?>(dto);
    }

    public Task<NewsReadStatsDto?> GetReadStatsAsync(
        Guid newsPostId,
        CancellationToken cancellationToken = default)
    {
        if (!Posts.TryGetValue(newsPostId, out var post))
        {
            return Task.FromResult<NewsReadStatsDto?>(null);
        }

        var audienceUsers = Users.Values
            .Where(x => x.User.Status == UserStatus.Active)
            .Select(x => x.User);

        if (post.Audience == NewsAudience.Drivers)
        {
            audienceUsers = audienceUsers.Where(u => u.Role == UserRole.Driver);
        }
        else if (post.Audience == NewsAudience.Dispatchers)
        {
            audienceUsers = audienceUsers.Where(u => u.Role == UserRole.Dispatcher);
        }

        var audienceList = audienceUsers.ToList();
        var totalAudience = audienceList.Count;

        var readCount = ReadReceipts
            .Where(r => r.NewsPostId == newsPostId && audienceList.Any(u => u.Id == r.UserId))
            .Select(r => r.UserId)
            .Distinct()
            .Count();

        var rate = totalAudience > 0 ? Math.Round((double)readCount / totalAudience, 4) : 0.0;

        return Task.FromResult<NewsReadStatsDto?>(new NewsReadStatsDto(newsPostId, readCount, totalAudience, rate));
    }

    public Task<IReadOnlyList<UnreadUserDto>> GetUnreadUsersAsync(
        Guid newsPostId,
        CancellationToken cancellationToken = default)
    {
        if (!Posts.TryGetValue(newsPostId, out var post))
        {
            return Task.FromResult<IReadOnlyList<UnreadUserDto>>(Array.Empty<UnreadUserDto>());
        }

        var audienceEntries = Users.Values
            .Where(x => x.User.Status == UserStatus.Active);

        if (post.Audience == NewsAudience.Drivers)
        {
            audienceEntries = audienceEntries.Where(x => x.User.Role == UserRole.Driver);
        }
        else if (post.Audience == NewsAudience.Dispatchers)
        {
            audienceEntries = audienceEntries.Where(x => x.User.Role == UserRole.Dispatcher);
        }

        var readUserIds = ReadReceipts
            .Where(r => r.NewsPostId == newsPostId)
            .Select(r => r.UserId)
            .ToHashSet();

        var unread = audienceEntries
            .Where(x => !readUserIds.Contains(x.User.Id))
            .OrderBy(x => x.User.DisplayName)
            .Select(x => new UnreadUserDto(
                x.User.Id,
                x.User.DisplayName,
                x.User.Email.Value,
                x.User.Role,
                x.EmployeeNo))
            .ToList();

        return Task.FromResult<IReadOnlyList<UnreadUserDto>>(unread);
    }
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }
    public bool ThrowOnSave { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave && ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        SaveChangesCallCount++;
        return Task.FromResult(1);
    }

    public Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IAsyncDisposable>(new NoOpAsyncDisposable());
    }

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class FakeDateTimeProvider(DateTimeOffset? fixedUtcNow = null) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = fixedUtcNow ?? new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
    public DateTimeOffset NzNow => UtcNow.ToOffset(TimeSpan.FromHours(12));
    public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
}
