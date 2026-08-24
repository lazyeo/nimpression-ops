using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.Commands.UpdateDriver;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Commands;

public sealed class UpdateDriverCommandHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UpdateDriverCommandHandler CreateSut()
    {
        return new UpdateDriverCommandHandler(_driverRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_with_existing_driver_updates_rates_licence_and_status()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            userId,
            "DRV-001",
            "Class 2",
            new DateOnly(2025, 1, 1),
            new Money(30m),
            new Money(40m),
            new Money(0.8m),
            "ENC(old_phone)",
            "ENC(old_addr)",
            "ENC(old_emg)",
            new DateOnly(2024, 1, 1),
            DriverStatus.Active);

        var user = new User(
            userId,
            new EmailAddress("driver@nimpression.co.nz"),
            "hash",
            UserRole.Driver,
            "Old Name",
            "en-NZ");

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>())
            .Returns(driver);
        _driverRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var command = new UpdateDriverCommand(
            driverId,
            "New Display Name",
            "Class 4",
            new DateOnly(2027, 12, 31),
            38.50m,
            "NZD",
            50.00m,
            "NZD",
            0.95m,
            "NZD",
            "+6421111222",
            "100 Queen St",
            "+6421999888",
            DriverStatus.Active);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        driver.LicenceClass.Should().Be("Class 4");
        driver.LicenceExpiry.Should().Be(new DateOnly(2027, 12, 31));
        driver.HourlyRate.Amount.Should().Be(38.50m);
        driver.PerTripRate.Amount.Should().Be(50.00m);
        driver.PerKmRate.Amount.Should().Be(0.95m);
        user.DisplayName.Should().Be("New Display Name");

        _driverRepository.Received(1).UpdateDriver(driver);
        _driverRepository.Received(1).UpdateUser(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_with_non_existent_driver_returns_not_found()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>())
            .Returns((Driver?)null);

        var command = new UpdateDriverCommand(
            driverId,
            "Name",
            "Class 4",
            new DateOnly(2027, 1, 1),
            30m,
            "NZD",
            40m,
            "NZD",
            0.8m,
            "NZD",
            "123",
            "Addr",
            "Emg",
            DriverStatus.Active);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("driver_not_found");
    }

    [Fact]
    public async Task Handle_setting_status_inactive_also_deactivates_user()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            userId,
            "DRV-001",
            "Class 2",
            new DateOnly(2025, 1, 1),
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
            "Name",
            "en-NZ");

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);
        _driverRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var command = new UpdateDriverCommand(
            driverId,
            "Name",
            "Class 2",
            new DateOnly(2025, 1, 1),
            30m,
            "NZD",
            40m,
            "NZD",
            0.8m,
            "NZD",
            "123",
            "Addr",
            "Emg",
            DriverStatus.Inactive);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        driver.Status.Should().Be(DriverStatus.Inactive);
        user.Status.Should().Be(UserStatus.Inactive);
    }
}
