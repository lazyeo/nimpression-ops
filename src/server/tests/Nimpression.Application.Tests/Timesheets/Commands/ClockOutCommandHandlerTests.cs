using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.Commands.ClockOut;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Timesheets.Commands;

public sealed class ClockOutCommandHandlerTests
{
    private readonly IShiftEntryRepository _shiftEntryRepository = Substitute.For<IShiftEntryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private ClockOutCommandHandler CreateSut()
    {
        _dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 8, 24, 17, 0, 0, TimeSpan.FromHours(12)));
        return new ClockOutCommandHandler(_shiftEntryRepository, _unitOfWork, _currentUser, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_driver_clock_out_by_active_shift_success()
    {
        // Arrange
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clockIn = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var clockOut = new DateTimeOffset(2026, 8, 24, 17, 0, 0, TimeSpan.FromHours(12));

        var existingShift = new ShiftEntry(Guid.NewGuid(), driverId, clockIn, -36.8485m, 174.7633m);

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(driverId);
        _shiftEntryRepository.GetActiveShiftByDriverIdAsync(driverId, Arg.Any<CancellationToken>())
            .Returns(existingShift);

        var command = new ClockOutCommand(
            ShiftId: null,
            ClockOutAt: clockOut,
            Latitude: -36.8500m,
            Longitude: 174.7600m,
            BreakMinutes: 30,
            Note: "Finished route on time");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingShift.Status.Should().Be(ShiftStatus.Completed);
        existingShift.ClockOutAt.Should().Be(clockOut);
        existingShift.ClockOutLat.Should().Be(-36.8500m);
        existingShift.ClockOutLng.Should().Be(174.7600m);
        existingShift.BreakMinutes.Should().Be(30);
        existingShift.Note.Should().Be("Finished route on time");

        _shiftEntryRepository.Received(1).Update(existingShift);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_clock_out_by_shift_id_success()
    {
        // Arrange
        var sut = CreateSut();
        var shiftId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var clockIn = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var existingShift = new ShiftEntry(shiftId, driverId, clockIn);

        _currentUser.Role.Returns(UserRole.Admin);
        _shiftEntryRepository.GetByIdAsync(shiftId, Arg.Any<CancellationToken>())
            .Returns(existingShift);

        var command = new ClockOutCommand(ShiftId: shiftId, BreakMinutes: 45);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingShift.Status.Should().Be(ShiftStatus.Completed);
        existingShift.BreakMinutes.Should().Be(45);
    }

    [Fact]
    public async Task Handle_clock_out_when_no_active_shift_returns_404_not_found()
    {
        // Arrange
        var sut = CreateSut();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(driverId);
        _shiftEntryRepository.GetActiveShiftByDriverIdAsync(driverId, Arg.Any<CancellationToken>())
            .Returns((ShiftEntry?)null);

        var command = new ClockOutCommand();

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("active_shift_not_found");
    }

    [Fact]
    public async Task Handle_clock_out_before_clock_in_returns_422_unprocessable()
    {
        // Arrange
        var sut = CreateSut();
        var shiftId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var clockIn = new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(12));
        var clockOut = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(12));
        var existingShift = new ShiftEntry(shiftId, driverId, clockIn);

        _currentUser.Role.Returns(UserRole.Admin);
        _shiftEntryRepository.GetByIdAsync(shiftId, Arg.Any<CancellationToken>())
            .Returns(existingShift);

        var command = new ClockOutCommand(ShiftId: shiftId, ClockOutAt: clockOut);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("clock_out_before_clock_in");
    }

    [Fact]
    public async Task Handle_clock_out_other_driver_shift_returns_403_forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var shiftId = Guid.NewGuid();
        var otherDriverId = Guid.NewGuid();
        var ownDriverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clockIn = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var existingShift = new ShiftEntry(shiftId, otherDriverId, clockIn);

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetByIdAsync(shiftId, Arg.Any<CancellationToken>())
            .Returns(existingShift);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ownDriverId);

        var command = new ClockOutCommand(ShiftId: shiftId);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }

    [Fact]
    public async Task Handle_driver_clock_out_by_other_driver_id_returns_403_forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var ownDriverId = Guid.NewGuid();
        var otherDriverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ownDriverId);

        var command = new ClockOutCommand(DriverId: otherDriverId);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }
}
