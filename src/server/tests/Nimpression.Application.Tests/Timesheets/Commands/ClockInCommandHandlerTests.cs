using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.Commands.ClockIn;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Timesheets.Commands;

public sealed class ClockInCommandHandlerTests
{
    private readonly IShiftEntryRepository _shiftEntryRepository = Substitute.For<IShiftEntryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private ClockInCommandHandler CreateSut()
    {
        _dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12)));
        return new ClockInCommandHandler(_shiftEntryRepository, _unitOfWork, _currentUser, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_driver_clock_in_success_with_gps()
    {
        // Arrange
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(driverId);
        _shiftEntryRepository.HasActiveShiftAsync(driverId, Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new ClockInCommand(
            DriverId: driverId,
            ClockInAt: null,
            Latitude: -36.8485m,
            Longitude: 174.7633m,
            VehicleId: vehicleId,
            LocationUnavailable: false);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        await _shiftEntryRepository.Received(1).AddAsync(
            Arg.Is<ShiftEntry>(s =>
                s.DriverId == driverId &&
                s.ClockInLat == -36.8485m &&
                s.ClockInLng == 174.7633m &&
                s.VehicleId == vehicleId &&
                s.Status == ShiftStatus.Active),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_driver_clock_in_with_location_unavailable_degrades_gracefully()
    {
        // Arrange (F6.1: 拒绝授权降级为无坐标，正常打卡成功)
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(driverId);
        _shiftEntryRepository.HasActiveShiftAsync(driverId, Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new ClockInCommand(
            DriverId: null,
            ClockInAt: null,
            Latitude: -36.8485m,
            Longitude: 174.7633m,
            LocationUnavailable: true);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _shiftEntryRepository.Received(1).AddAsync(
            Arg.Is<ShiftEntry>(s =>
                s.DriverId == driverId &&
                s.ClockInLat == null &&
                s.ClockInLng == null &&
                s.Status == ShiftStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_active_shift_exists_returns_409_conflict()
    {
        // Arrange (F6.1: 未下班时再次打卡返回 409)
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(driverId);
        _shiftEntryRepository.HasActiveShiftAsync(driverId, Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new ClockInCommand();

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be("active_shift_conflict");

        await _shiftEntryRepository.DidNotReceive().AddAsync(Arg.Any<ShiftEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_driver_clock_in_for_other_driver_returns_403_forbidden()
    {
        // Arrange (IDOR 防护：司机不可替他人打卡)
        var sut = CreateSut();
        var ownDriverId = Guid.NewGuid();
        var otherDriverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ownDriverId);

        var command = new ClockInCommand(DriverId: otherDriverId);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }

    [Fact]
    public async Task Handle_admin_clock_in_for_driver_success()
    {
        // Arrange
        var sut = CreateSut();
        var driverId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Admin);
        _shiftEntryRepository.DriverExistsAsync(driverId, Arg.Any<CancellationToken>())
            .Returns(true);
        _shiftEntryRepository.HasActiveShiftAsync(driverId, Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new ClockInCommand(DriverId: driverId);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _shiftEntryRepository.Received(1).AddAsync(
            Arg.Is<ShiftEntry>(s => s.DriverId == driverId),
            Arg.Any<CancellationToken>());
    }
}
