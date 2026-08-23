using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.Queries.CheckDriverDispatchEligibility;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Queries;

public sealed class CheckDriverDispatchEligibilityQueryHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private CheckDriverDispatchEligibilityQueryHandler CreateSut()
    {
        _dateTimeProvider.NzToday.Returns(new DateOnly(2026, 8, 24));
        return new CheckDriverDispatchEligibilityQueryHandler(_driverRepository, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_active_driver_with_valid_licence_can_be_dispatched()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            Guid.NewGuid(),
            "DRV-001",
            "Class 4",
            new DateOnly(2027, 8, 24),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "phone",
            "addr",
            "emg",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);

        var query = new CheckDriverDispatchEligibilityQuery(driverId);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CanBeDispatched.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_expired_licence_returns_422_with_explicit_reason()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            Guid.NewGuid(),
            "DRV-004",
            "Class 2",
            new DateOnly(2026, 8, 20), // Expired 4 days ago
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "phone",
            "addr",
            "emg",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);

        var query = new CheckDriverDispatchEligibilityQuery(driverId);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("driver_licence_expired");
        result.Error.Message.Should().Contain("2026-08-20");
    }

    [Fact]
    public async Task Handle_inactive_driver_returns_422_with_explicit_reason()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            Guid.NewGuid(),
            "DRV-005",
            "Class 4",
            new DateOnly(2027, 8, 20),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "phone",
            "addr",
            "emg",
            new DateOnly(2024, 1, 1),
            DriverStatus.Inactive);

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);

        var query = new CheckDriverDispatchEligibilityQuery(driverId);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("driver_not_active");
    }

    [Fact]
    public async Task Handle_non_existent_driver_returns_not_found_404()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns((Driver?)null);

        var query = new CheckDriverDispatchEligibilityQuery(driverId);
        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("driver_not_found");
    }
}
