using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.Commands.CreateNewsPost;
using Nimpression.Application.Features.News.Commands.MarkNewsAsRead;
using Nimpression.Application.Features.News.DTOs;
using Nimpression.Application.Features.News.Queries.GetNewsById;
using Nimpression.Application.Features.News.Queries.GetNewsList;
using Nimpression.Application.Features.News.Queries.GetNewsReadStats;
using Nimpression.Application.Features.News.Queries.GetNewsUnreadUsers;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Repositories;
using Nimpression.Integration.Tests.Fixtures;
using Xunit;

namespace Nimpression.Integration.Tests.News;

[Collection("PostgreSqlCollection")]
public class NewsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly TestDateTimeProvider _dtProvider = new();

    public NewsIntegrationTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var context = _fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<User> SeedUserAsync(UserRole role, UserStatus status = UserStatus.Active, string? employeeNo = null)
    {
        await using var context = _fixture.CreateDbContext();

        var user = new User(
            Guid.NewGuid(),
            TestDataFactory.CreateEmailAddress("news_user"),
            "hash",
            role,
            $"Test {role} {Guid.NewGuid().ToString("N")[..4]}",
            "en-NZ",
            _dtProvider.UtcNow);

        user.SetStatus(status);
        await context.Users.AddAsync(user);

        if (role == UserRole.Driver)
        {
            var driver = new Driver(
                Guid.NewGuid(),
                user.Id,
                employeeNo ?? TestDataFactory.CreateEmployeeNo("DRV"),
                "Class 4",
                new DateOnly(2028, 1, 1),
                new Money(32m),
                new Money(45m),
                new Money(0.85m),
                "ENC(021000000)",
                "ENC(123 Road)",
                "ENC(Contact)",
                new DateOnly(2024, 1, 1),
                status == UserStatus.Active ? DriverStatus.Active : DriverStatus.Inactive);

            await context.Drivers.AddAsync(driver);
        }

        await context.SaveChangesAsync();
        return user;
    }

    #region F10.1 发布与查询

    [Fact]
    public async Task F10_1_AdminPublishNews_WithBilingualContent_PersistsToDatabaseAndReturnsId()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.Admin);

        await using var context = _fixture.CreateDbContext();
        var repo = new NewsRepository(context);
        var uow = new UnitOfWork(context);
        var currentUser = new TestCurrentUser(admin.Id, UserRole.Admin);
        var handler = new CreateNewsPostCommandHandler(repo, uow, currentUser, _dtProvider);

        var command = new CreateNewsPostCommand(
            "Road Closure Update",
            "State Highway 1 will undergo essential repairs this weekend.",
            "1号国道将于本周末进行必要施工维修，请注意绕行。",
            NewsAudience.All,
            Pinned: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        await using var verifyContext = _fixture.CreateDbContext();
        var saved = await verifyContext.NewsPosts.FindAsync(result.Value);
        saved.Should().NotBeNull();
        saved!.Title.Should().Be("Road Closure Update");
        saved.BodyEn.Should().Be("State Highway 1 will undergo essential repairs this weekend.");
        saved.BodyZh.Should().Be("1号国道将于本周末进行必要施工维修，请注意绕行。");
        saved.Audience.Should().Be(NewsAudience.All);
        saved.Pinned.Should().BeTrue();
        saved.IsActive.Should().BeTrue();
        saved.PublishedAt.Should().Be(_dtProvider.UtcNow);
    }

    [Theory]
    [InlineData("", "Body En", "Body Zh")]
    [InlineData("Title", "", "Body Zh")]
    [InlineData("Title", "   ", "Body Zh")]
    [InlineData("Title", "Body En", "")]
    [InlineData("Title", "Body En", "   ")]
    public async Task F10_1_PublishNews_MissingEnOrZhBody_Returns422Unprocessable(string title, string bodyEn, string bodyZh)
    {
        // Arrange: 双语正文两个字段都必填，缺一 422
        var admin = await SeedUserAsync(UserRole.Admin);

        await using var context = _fixture.CreateDbContext();
        var repo = new NewsRepository(context);
        var uow = new UnitOfWork(context);
        var currentUser = new TestCurrentUser(admin.Id, UserRole.Admin);
        var handler = new CreateNewsPostCommandHandler(repo, uow, currentUser, _dtProvider);

        var command = new CreateNewsPostCommand(
            title,
            bodyEn,
            bodyZh,
            NewsAudience.All);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
    }

    [Fact]
    public async Task F10_1_NewsList_OrderByPinnedFirstThenPublishedAtDescending_AndProjected()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.Admin);
        var driver = await SeedUserAsync(UserRole.Driver);

        await using var context = _fixture.CreateDbContext();
        var repo = new NewsRepository(context);
        var uow = new UnitOfWork(context);

        // 创建3篇新闻：普通较新、置顶较旧、置顶最新
        var normalNewer = new NewsPost(
            Guid.NewGuid(), admin.Id, "Normal Newer", "En", "Zh", NewsAudience.All, _dtProvider.UtcNow.AddHours(-1), pinned: false);
        var pinnedOlder = new NewsPost(
            Guid.NewGuid(), admin.Id, "Pinned Older", "En", "Zh", NewsAudience.All, _dtProvider.UtcNow.AddHours(-10), pinned: true);
        var pinnedNewest = new NewsPost(
            Guid.NewGuid(), admin.Id, "Pinned Newest", "En", "Zh", NewsAudience.All, _dtProvider.UtcNow.AddMinutes(-5), pinned: true);

        await context.NewsPosts.AddRangeAsync(normalNewer, pinnedOlder, pinnedNewest);
        // driver 读过 pinnedNewest
        await context.NewsReadReceipts.AddAsync(new NewsReadReceipt(Guid.NewGuid(), pinnedNewest.Id, driver.Id, _dtProvider.UtcNow));
        await context.SaveChangesAsync();

        var currentUser = new TestCurrentUser(driver.Id, UserRole.Driver);
        var queryHandler = new GetNewsListQueryHandler(repo, currentUser);

        // Act
        var result = await queryHandler.Handle(new GetNewsListQuery(new NewsListFilter()), CancellationToken.None);

        // Assert: 置顶优先（pinnedNewest > pinnedOlder），随后普通（normalNewer）
        result.IsSuccess.Should().BeTrue();
        var items = result.Value.Items.Where(i => i.Id == normalNewer.Id || i.Id == pinnedOlder.Id || i.Id == pinnedNewest.Id).ToList();
        items.Should().HaveCount(3);
        items[0].Id.Should().Be(pinnedNewest.Id);
        items[0].Pinned.Should().BeTrue();
        items[0].IsRead.Should().BeTrue();

        items[1].Id.Should().Be(pinnedOlder.Id);
        items[1].Pinned.Should().BeTrue();
        items[1].IsRead.Should().BeFalse();

        items[2].Id.Should().Be(normalNewer.Id);
        items[2].Pinned.Should().BeFalse();
        items[2].IsRead.Should().BeFalse();
    }

    #endregion

    #region F10.2 已读回执与统计

    /// <summary>
    /// AC F10.2 坑 1：同一人重复打开不能产生多条回执 —— 靠唯一约束冲突判重，不许先查后写。
    /// </summary>
    [Fact]
    public async Task F10_2_MarkNewsAsRead_DuplicateOpenBySameUser_ProducesSingleReceiptIdempotently()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.Admin);
        var driver = await SeedUserAsync(UserRole.Driver);

        await using var context = _fixture.CreateDbContext();
        var post = new NewsPost(
            Guid.NewGuid(), admin.Id, "Safety Briefing", "En", "Zh", NewsAudience.Drivers, _dtProvider.UtcNow);
        await context.NewsPosts.AddAsync(post);
        await context.SaveChangesAsync();

        var repo = new NewsRepository(context);
        var uow = new UnitOfWork(context);
        var currentUser = new TestCurrentUser(driver.Id, UserRole.Driver);
        var handler = new MarkNewsAsReadCommandHandler(repo, uow, currentUser, _dtProvider);

        // Act 1: 第一次打开并标记已读
        var result1 = await handler.Handle(new MarkNewsAsReadCommand(post.Id), CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        // Act 2: 同一人在新的请求上下文中重复打开该新闻（命中 PostgreSQL IX_NewsReadReceipts_NewsPostId_UserId 唯一索引冲突 23505）
        await using var context2 = _fixture.CreateDbContext();
        var repo2 = new NewsRepository(context2);
        var uow2 = new UnitOfWork(context2);
        var handler2 = new MarkNewsAsReadCommandHandler(repo2, uow2, currentUser, _dtProvider);

        var result2 = await handler2.Handle(new MarkNewsAsReadCommand(post.Id), CancellationToken.None);

        // Assert: 幂等成功，数据库中仅有 1 条已读回执
        result2.IsSuccess.Should().BeTrue();

        await using var verifyContext = _fixture.CreateDbContext();
        var receiptsCount = await verifyContext.NewsReadReceipts
            .CountAsync(r => r.NewsPostId == post.Id && r.UserId == driver.Id);
        receiptsCount.Should().Be(1, "The unique index constraint must prevent duplicate read receipts from being inserted.");
    }

    /// <summary>
    /// AC F10.2 坑 2：「已读 7/10」的分母是受众范围内人数且排除停用账号。
    /// </summary>
    [Fact]
    public async Task F10_2_GetNewsReadStats_DenominatorExcludesInactiveUsers_AndMatchesAudienceScope()
    {
        // Arrange:
        // 创建管理员 1 名
        var admin = await SeedUserAsync(UserRole.Admin);

        // 创建仅司机受众的新闻
        await using var initContext = _fixture.CreateDbContext();
        var newsPost = new NewsPost(
            Guid.NewGuid(), admin.Id, "Drivers Important Update", "En", "Zh", NewsAudience.Drivers, _dtProvider.UtcNow);
        await initContext.NewsPosts.AddAsync(newsPost);
        await initContext.SaveChangesAsync();

        // 构造受众环境：
        // 10 名司机：7 名 Active，3 名 Inactive（停用/离职）
        var activeDrivers = new List<User>();
        for (var i = 1; i <= 7; i++)
        {
            var drv = await SeedUserAsync(UserRole.Driver, UserStatus.Active, $"DRV-ACT-{Guid.NewGuid().ToString("N")[..4]}");
            activeDrivers.Add(drv);
        }

        var inactiveDrivers = new List<User>();
        for (var i = 1; i <= 3; i++)
        {
            var drv = await SeedUserAsync(UserRole.Driver, UserStatus.Inactive, $"DRV-INA-{Guid.NewGuid().ToString("N")[..4]}");
            inactiveDrivers.Add(drv);
        }

        // 创建 2 名其他角色激活用户（调度员 / 管理员），他们不在 Drivers 受众范围内
        var dispatcher = await SeedUserAsync(UserRole.Dispatcher, UserStatus.Active);

        // 记录已读回执：
        // 7 名活跃司机中有 5 名已读
        await using var receiptContext = _fixture.CreateDbContext();
        for (var i = 0; i < 5; i++)
        {
            await receiptContext.NewsReadReceipts.AddAsync(
                new NewsReadReceipt(Guid.NewGuid(), newsPost.Id, activeDrivers[i].Id, _dtProvider.UtcNow));
        }

        // 停用司机也有历史回执（离职前读过）
        await receiptContext.NewsReadReceipts.AddAsync(
            new NewsReadReceipt(Guid.NewGuid(), newsPost.Id, inactiveDrivers[0].Id, _dtProvider.UtcNow));

        await receiptContext.SaveChangesAsync();

        // Act: 管理员查询统计
        await using var queryContext = _fixture.CreateDbContext();
        var totalActiveDriversInDb = await queryContext.Users
            .CountAsync(u => u.Role == UserRole.Driver && u.Status == UserStatus.Active);
        var totalInactiveDriversInDb = await queryContext.Users
            .CountAsync(u => u.Role == UserRole.Driver && u.Status != UserStatus.Active);

        var repo = new NewsRepository(queryContext);
        var currentUser = new TestCurrentUser(admin.Id, UserRole.Admin);
        var statsHandler = new GetNewsReadStatsQueryHandler(repo, currentUser);
        var unreadHandler = new GetNewsUnreadUsersQueryHandler(repo, currentUser);

        var statsResult = await statsHandler.Handle(new GetNewsReadStatsQuery(newsPost.Id), CancellationToken.None);
        var unreadResult = await unreadHandler.Handle(new GetNewsUnreadUsersQuery(newsPost.Id), CancellationToken.None);

        // Assert:
        // 分母应为当前有效受众中处于激活状态的司机总数（排除了停用司机与非司机角色）
        statsResult.IsSuccess.Should().BeTrue();
        statsResult.Value.TargetAudienceCount.Should().Be(totalActiveDriversInDb, "Denominator must only include active users within the target audience scope (excluding deactivated accounts).");
        totalInactiveDriversInDb.Should().BeGreaterThanOrEqualTo(3);
        statsResult.Value.ReadCount.Should().Be(5);
        statsResult.Value.ReadRate.Should().BeApproximately(5.0 / totalActiveDriversInDb, 0.0001);

        // 未读名单：包含剩下的未读活跃司机，不含停用司机与非司机
        unreadResult.IsSuccess.Should().BeTrue();
        var unreadIds = unreadResult.Value.Select(u => u.UserId).ToList();
        unreadIds.Should().Contain(activeDrivers[5].Id);
        unreadIds.Should().Contain(activeDrivers[6].Id);
        unreadIds.Should().NotContain(inactiveDrivers.Select(d => d.Id));
        unreadIds.Should().NotContain(dispatcher.Id);
    }

    #endregion

    #region F10.3 领域事件分发

    [Fact]
    public async Task F10_3_PublishNews_DispatchesNewsPublishedDomainEventToOutbox()
    {
        // Arrange
        var admin = await SeedUserAsync(UserRole.Admin);

        await using var context = _fixture.CreateDbContext();
        var repo = new NewsRepository(context);
        var uow = new UnitOfWork(context);
        var currentUser = new TestCurrentUser(admin.Id, UserRole.Admin);
        var handler = new CreateNewsPostCommandHandler(repo, uow, currentUser, _dtProvider);

        var command = new CreateNewsPostCommand(
            "Fleet Protocol Update",
            "New protocol for emergency stops.",
            "紧急制动新操作规范已下发。",
            NewsAudience.Drivers);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await using var verifyContext = _fixture.CreateDbContext();
        var outbox = await verifyContext.OutboxMessages
            .Where(m => m.Type == "NewsPublished" && m.PayloadJson.Contains(result.Value.ToString()))
            .FirstOrDefaultAsync();

        outbox.Should().NotBeNull("NewsPublished domain event must be written to Outbox for asynchronous SignalR notification.");
        outbox!.PayloadJson.Should().Contain(result.Value.ToString());
    }

    #endregion

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset NzNow => UtcNow.ToOffset(TimeSpan.FromHours(12));
        public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
    }

    private sealed class TestCurrentUser(Guid userId, UserRole role) : ICurrentUser
    {
        public Guid? UserId => userId;
        public UserRole? Role => role;
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "IntegrationTestAgent";
        public bool IsAuthenticated => true;
    }
}
