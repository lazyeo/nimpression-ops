using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Application.Features.Realtime.Queries.GetRecentChanges;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Realtime;

public sealed class GetRecentChangesQueryHandlerTests
{
    private readonly IRealtimeChangesRepository _changesRepo = Substitute.For<IRealtimeChangesRepository>();
    private readonly IDriverRepository _driverRepo = Substitute.For<IDriverRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private static readonly DateTimeOffset FixedNow = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly FixedToday = new(2026, 8, 24);

    [Fact]
    public async Task Handle_UnauthenticatedUser_ReturnsUnauthorizedError()
    {
        // Arrange
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.UserId.Returns((Guid?)null);

        var handler = new GetRecentChangesQueryHandler(_changesRepo, _driverRepo, _currentUser);
        var query = new GetRecentChangesQuery(FixedNow.AddMinutes(-5));

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("AUTH_UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_DriverUser_ResolvesDriverId_AndQueriesFilteredChanges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var since = FixedNow.AddMinutes(-10);

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _currentUser.Role.Returns(UserRole.Driver);

        var dummyDriver = new Driver(
            driverId,
            userId,
            "DRV-001001",
            "Class 2",
            FixedToday.AddYears(1),
            new Money(25m),
            new Money(15m),
            new Money(1.5m),
            "phoneEnc",
            "addressEnc",
            "emergencyEnc",
            FixedToday);

        _driverRepo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(dummyDriver);

        var expectedChanges = new List<RealtimeChangeDto>
        {
            new("task.assigned", Guid.NewGuid(), FixedNow)
        };

        _changesRepo.GetChangesSinceAsync(since, driverId, UserRole.Driver, 100, Arg.Any<CancellationToken>())
            .Returns(expectedChanges);

        var handler = new GetRecentChangesQueryHandler(_changesRepo, _driverRepo, _currentUser);
        var query = new GetRecentChangesQuery(since);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedChanges);
    }

    [Fact]
    public async Task Handle_DispatcherUser_QueriesWithDispatcherRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var since = FixedNow.AddMinutes(-10);

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _currentUser.Role.Returns(UserRole.Dispatcher);

        var expectedChanges = new List<RealtimeChangeDto>
        {
            new("incident.reported", Guid.NewGuid(), FixedNow),
            new("task.completed", Guid.NewGuid(), FixedNow)
        };

        _changesRepo.GetChangesSinceAsync(since, null, UserRole.Dispatcher, 50, Arg.Any<CancellationToken>())
            .Returns(expectedChanges);

        var handler = new GetRecentChangesQueryHandler(_changesRepo, _driverRepo, _currentUser);
        var query = new GetRecentChangesQuery(since, Limit: 50);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }
}
