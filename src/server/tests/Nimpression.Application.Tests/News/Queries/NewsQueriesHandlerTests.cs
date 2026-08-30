using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.News.DTOs;
using Nimpression.Application.Features.News.Queries.GetNewsById;
using Nimpression.Application.Features.News.Queries.GetNewsList;
using Nimpression.Application.Features.News.Queries.GetNewsReadStats;
using Nimpression.Application.Features.News.Queries.GetNewsUnreadUsers;
using Nimpression.Application.Tests.News.TestDoubles;
using Nimpression.Domain.Entities.Communications;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.News.Queries;

public class NewsQueriesHandlerTests
{
    private readonly FakeNewsRepository _repo = new();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly FakeDateTimeProvider _dateTimeProvider = new();

    public NewsQueriesHandlerTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(Guid.NewGuid());
        _currentUser.Role.Returns(UserRole.Admin);
    }

    private User CreateUser(Guid id, string email, UserRole role, UserStatus status = UserStatus.Active, string name = "User")
    {
        var user = new User(id, new EmailAddress(email), "hash", role, name, "en-NZ", _dateTimeProvider.UtcNow);
        user.SetStatus(status);
        return user;
    }

    #region F10.1 List & Detail Queries

    [Fact]
    public async Task GetNewsList_DriverAudience_FiltersOutDispatcherNewsAndOrdersByPinnedFirst()
    {
        // Arrange: 3 篇公告（全员、仅司机、仅调度员，部分置顶）
        var postAll = new NewsPost(Guid.NewGuid(), _currentUser.UserId!.Value, "All News", "En", "Zh", NewsAudience.All, _dateTimeProvider.UtcNow.AddHours(-2), pinned: false);
        var postDrvPinned = new NewsPost(Guid.NewGuid(), _currentUser.UserId!.Value, "Driver Pinned News", "En", "Zh", NewsAudience.Drivers, _dateTimeProvider.UtcNow.AddHours(-5), pinned: true);
        var postDsp = new NewsPost(Guid.NewGuid(), _currentUser.UserId!.Value, "Dispatcher News", "En", "Zh", NewsAudience.Dispatchers, _dateTimeProvider.UtcNow.AddHours(-1), pinned: false);

        _repo.Posts[postAll.Id] = postAll;
        _repo.Posts[postDrvPinned.Id] = postDrvPinned;
        _repo.Posts[postDsp.Id] = postDsp;

        _currentUser.Role.Returns(UserRole.Driver);
        var handler = new GetNewsListQueryHandler(_repo, _currentUser);

        // Act
        var result = await handler.Handle(new GetNewsListQuery(new NewsListFilter()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        // 置顶优先：postDrvPinned 在第一位
        result.Value.Items[0].Id.Should().Be(postDrvPinned.Id);
        result.Value.Items[1].Id.Should().Be(postAll.Id);
    }

    [Fact]
    public async Task GetNewsById_DriverAccessingDispatcherOnlyNews_Returns403Forbidden()
    {
        // Arrange
        var postDsp = new NewsPost(Guid.NewGuid(), Guid.NewGuid(), "Dispatcher News", "En", "Zh", NewsAudience.Dispatchers, _dateTimeProvider.UtcNow);
        _repo.Posts[postDsp.Id] = postDsp;

        _currentUser.Role.Returns(UserRole.Driver);
        var handler = new GetNewsByIdQueryHandler(_repo, _currentUser);

        // Act
        var result = await handler.Handle(new GetNewsByIdQuery(postDsp.Id), CancellationToken.None);

        // Assert: 越权访问返回 403 而非 404
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task GetNewsById_NewsNotFound_Returns404NotFound()
    {
        // Arrange
        var handler = new GetNewsByIdQueryHandler(_repo, _currentUser);

        // Act
        var result = await handler.Handle(new GetNewsByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    #endregion

    #region F10.2 Read Stats & Unread Users Queries

    [Fact]
    public async Task GetNewsReadStats_DriversOnlyAudience_CalculatesCorrectDenominatorExcludingInactiveUsers()
    {
        // Arrange:
        // 创建 10 名司机（7 人 Active，3 人 Inactive / Suspended）
        // 创建 2 名管理员（Active，但不属于 Drivers 受众）
        var newsPost = new NewsPost(Guid.NewGuid(), _currentUser.UserId!.Value, "Driver Notice", "En", "Zh", NewsAudience.Drivers, _dateTimeProvider.UtcNow);
        _repo.Posts[newsPost.Id] = newsPost;

        var activeDrivers = new List<User>();
        for (var i = 1; i <= 7; i++)
        {
            var drv = CreateUser(Guid.NewGuid(), $"driver{i}@test.co.nz", UserRole.Driver, UserStatus.Active, $"Active Driver {i}");
            activeDrivers.Add(drv);
            _repo.Users[drv.Id] = (drv, $"DRV-00{i}");
        }

        for (var i = 8; i <= 10; i++)
        {
            var inactiveDrv = CreateUser(Guid.NewGuid(), $"inactive{i}@test.co.nz", UserRole.Driver, UserStatus.Inactive, $"Inactive Driver {i}");
            _repo.Users[inactiveDrv.Id] = (inactiveDrv, $"DRV-00{i}");
        }

        var admin = CreateUser(Guid.NewGuid(), "admin@test.co.nz", UserRole.Admin, UserStatus.Active, "Admin User");
        _repo.Users[admin.Id] = (admin, null);

        // 假设前 5 名活跃司机已读
        for (var i = 0; i < 5; i++)
        {
            _repo.ReadReceipts.Add(new NewsReadReceipt(Guid.NewGuid(), newsPost.Id, activeDrivers[i].Id, _dateTimeProvider.UtcNow));
        }

        // 停用司机也有回执（例如离职前读过）
        _repo.ReadReceipts.Add(new NewsReadReceipt(Guid.NewGuid(), newsPost.Id, _repo.Users.Values.First(u => u.User.Status == UserStatus.Inactive).User.Id, _dateTimeProvider.UtcNow));

        var handler = new GetNewsReadStatsQueryHandler(_repo, _currentUser);

        // Act
        var result = await handler.Handle(new GetNewsReadStatsQuery(newsPost.Id), CancellationToken.None);

        // Assert:
        // 分母应为 7（排除 3 名停用司机，排除管理员），分子应为 5（已读活跃司机）
        result.IsSuccess.Should().BeTrue();
        result.Value.NewsPostId.Should().Be(newsPost.Id);
        result.Value.TargetAudienceCount.Should().Be(7);
        result.Value.ReadCount.Should().Be(5);
        result.Value.ReadRate.Should().BeApproximately(5.0 / 7.0, 0.0001);
    }

    [Fact]
    public async Task GetNewsUnreadUsers_ReturnsOnlyActiveUnreadUsersInTargetAudience()
    {
        // Arrange
        var newsPost = new NewsPost(Guid.NewGuid(), _currentUser.UserId!.Value, "Driver Notice", "En", "Zh", NewsAudience.Drivers, _dateTimeProvider.UtcNow);
        _repo.Posts[newsPost.Id] = newsPost;

        var drv1 = CreateUser(Guid.NewGuid(), "drv1@test.co.nz", UserRole.Driver, UserStatus.Active, "Driver 1");
        var drv2 = CreateUser(Guid.NewGuid(), "drv2@test.co.nz", UserRole.Driver, UserStatus.Active, "Driver 2");
        var drvInactive = CreateUser(Guid.NewGuid(), "drv_in@test.co.nz", UserRole.Driver, UserStatus.Inactive, "Inactive Driver");

        _repo.Users[drv1.Id] = (drv1, "DRV-1");
        _repo.Users[drv2.Id] = (drv2, "DRV-2");
        _repo.Users[drvInactive.Id] = (drvInactive, "DRV-IN");

        // drv1 已读
        _repo.ReadReceipts.Add(new NewsReadReceipt(Guid.NewGuid(), newsPost.Id, drv1.Id, _dateTimeProvider.UtcNow));

        var handler = new GetNewsUnreadUsersQueryHandler(_repo, _currentUser);

        // Act
        var result = await handler.Handle(new GetNewsUnreadUsersQuery(newsPost.Id), CancellationToken.None);

        // Assert: 只有 drv2 在未读名单中（drv1 已读，drvInactive 停用不计入）
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].UserId.Should().Be(drv2.Id);
        result.Value[0].DisplayName.Should().Be("Driver 2");
        result.Value[0].EmployeeNo.Should().Be("DRV-2");
    }

    [Fact]
    public async Task GetNewsReadStats_DriverRole_Returns403Forbidden()
    {
        // Arrange
        _currentUser.Role.Returns(UserRole.Driver);
        var handler = new GetNewsReadStatsQueryHandler(_repo, _currentUser);

        // Act
        var result = await handler.Handle(new GetNewsReadStatsQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    #endregion
}
