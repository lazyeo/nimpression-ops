using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.Commands.UpdateDriverSelfProfile;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Drivers.Commands;

public sealed class UpdateDriverSelfProfileCommandHandlerTests
{
    private readonly IDriverRepository _driverRepository = Substitute.For<IDriverRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UpdateDriverSelfProfileCommandHandler CreateSut()
    {
        return new UpdateDriverSelfProfileCommandHandler(
            _driverRepository,
            _currentUser,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_driver_updates_own_phone_emergency_locale_successfully()
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
            "Driver Name",
            "en-NZ");

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);
        _driverRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);

        var command = new UpdateDriverSelfProfileCommand(
            driverId,
            "+6421999111",
            "+6421888222",
            "zh-CN",
            "50 Queen St");

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        driver.PhoneEnc.Should().Be("ENC(+6421999111)");
        driver.EmergencyContactEnc.Should().Be("ENC(+6421888222)");
        driver.AddressEnc.Should().Be("ENC(50 Queen St)");
        user.Locale.Should().Be("zh-CN");

        _driverRepository.Received(1).UpdateDriver(driver);
        _driverRepository.Received(1).UpdateUser(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("DRV-999", null, null, null, null)] // Attempted employee no
    [InlineData(null, 50.0, null, null, null)]     // Attempted hourly rate
    [InlineData(null, null, 60.0, null, null)]     // Attempted per trip rate
    [InlineData(null, null, null, 1.20, null)]     // Attempted per km rate
    [InlineData(null, null, null, null, DriverStatus.Inactive)] // Attempted status
    public async Task Handle_attempting_to_modify_forbidden_fields_returns_403(
        string? employeeNo,
        double? hourlyRate,
        double? perTripRate,
        double? perKmRate,
        DriverStatus? status)
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        var command = new UpdateDriverSelfProfileCommand(
            driverId,
            "+6421999111",
            "+6421888222",
            "en-NZ",
            null,
            employeeNo,
            hourlyRate.HasValue ? (decimal)hourlyRate.Value : null,
            perTripRate.HasValue ? (decimal)perTripRate.Value : null,
            perKmRate.HasValue ? (decimal)perKmRate.Value : null,
            status);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden_field_modification");
    }

    [Fact]
    public async Task Handle_driver_updating_another_drivers_profile_returns_forbidden_403()
    {
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var driverUserId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();

        var driver = new Driver(
            driverId,
            driverUserId,
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

        _driverRepository.GetByIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(driver);
        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(differentUserId);

        var command = new UpdateDriverSelfProfileCommand(
            driverId,
            "+6421999111",
            "+6421888222",
            "en-NZ");

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }
}
