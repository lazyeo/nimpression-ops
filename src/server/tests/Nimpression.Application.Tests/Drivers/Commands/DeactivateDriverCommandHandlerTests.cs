using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.Commands.DeactivateDriver;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Events;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Commands;

public sealed class DeactivateDriverCommandHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private DeactivateDriverCommandHandler CreateSut()
    {
        _dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
        return new DeactivateDriverCommandHandler(_driverRepository, _unitOfWork, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_deactivates_driver_and_sets_user_status_inactive()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            userId,
            "DRV-001",
            "Class 4",
            new DateOnly(2027, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "phone",
            "addr",
            "emg",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        var user = new User(
            userId,
            new EmailAddress("driver@nimpression.co.nz"),
            "hash",
            UserRole.Driver,
            "Driver Name",
            "en-NZ");

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);
        _driverRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new DeactivateDriverCommand(driverId, "Resigned");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        driver.Status.Should().Be(DriverStatus.Inactive);
        user.Status.Should().Be(UserStatus.Inactive);

        // 验证产出 DriverDeactivated 领域事件
        driver.DomainEvents.Should().ContainSingle(e => e is DriverDeactivated);

        _driverRepository.Received(1).UpdateDriver(driver);
        _driverRepository.Received(1).UpdateUser(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_with_non_existent_driver_returns_not_found()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns((Driver?)null);

        var command = new DeactivateDriverCommand(driverId);
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("driver_not_found");
    }
}
