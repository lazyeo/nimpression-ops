using System.Text.Json;
using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.Commands.AdminCorrectShift;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Timesheets.Commands;

public sealed class AdminCorrectShiftCommandHandlerTests
{
    private readonly IShiftEntryRepository _shiftEntryRepository = Substitute.For<IShiftEntryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IAuditSink _auditSink = Substitute.For<IAuditSink>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private AdminCorrectShiftCommandHandler CreateSut()
    {
        _dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.FromHours(12)));
        return new AdminCorrectShiftCommandHandler(
            _shiftEntryRepository,
            _unitOfWork,
            _currentUser,
            _auditSink,
            _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_admin_correction_success_updates_shift_and_records_full_audit()
    {
        // Arrange (F6.4: Admin 改打卡记录，理由齐全，原值新值全量入审计)
        var sut = CreateSut();
        var shiftId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var origIn = new DateTimeOffset(2026, 8, 24, 8, 30, 0, TimeSpan.FromHours(12));
        var origOut = new DateTimeOffset(2026, 8, 24, 17, 0, 0, TimeSpan.FromHours(12));

        var existingShift = new ShiftEntry(shiftId, driverId, origIn);
        existingShift.ClockOut(origOut, breakMinutes: 30);

        _currentUser.Role.Returns(UserRole.Admin);
        _currentUser.UserId.Returns(adminId);
        _shiftEntryRepository.GetByIdAsync(shiftId, Arg.Any<CancellationToken>())
            .Returns(existingShift);

        var newIn = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var newOut = new DateTimeOffset(2026, 8, 24, 17, 30, 0, TimeSpan.FromHours(12));
        const string reason = "Driver clocked in late due to depot gate hardware failure";

        var command = new AdminCorrectShiftCommand(
            ShiftId: shiftId,
            NewClockInAt: newIn,
            NewClockOutAt: newOut,
            NewBreakMinutes: 45,
            Reason: reason);

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingShift.ClockInAt.Should().Be(newIn);
        existingShift.ClockOutAt.Should().Be(newOut);
        existingShift.BreakMinutes.Should().Be(45);
        existingShift.AdminCorrectionReason.Should().Be(reason);
        existingShift.CorrectedByUserId.Should().Be(adminId);

        _shiftEntryRepository.Received(1).Update(existingShift);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // 验证全量审计记录
        await _auditSink.Received(1).RecordAsync(
            "ShiftEntry",
            shiftId,
            "AdminCorrectShift",
            Arg.Is<string>(b => b.Contains("08:30:00") && b.Contains("17:00:00")),
            Arg.Is<string>(a => a.Contains("08:00:00") && a.Contains("17:30:00") && a.Contains("depot gate")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_missing_reason_returns_422_unprocessable()
    {
        // Arrange (F6.4: 缺理由 422)
        var sut = CreateSut();
        var shiftId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Admin);

        var command = new AdminCorrectShiftCommand(
            ShiftId: shiftId,
            NewClockInAt: DateTimeOffset.UtcNow,
            NewClockOutAt: DateTimeOffset.UtcNow.AddHours(8),
            NewBreakMinutes: 30,
            Reason: "   "); // Empty or whitespace

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.UnprocessableEntity);
        result.Error.Code.Should().Be("correction_reason_required");

        await _shiftEntryRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_non_admin_returns_403_forbidden()
    {
        // Arrange
        var sut = CreateSut();
        var shiftId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Driver);

        var command = new AdminCorrectShiftCommand(
            ShiftId: shiftId,
            NewClockInAt: DateTimeOffset.UtcNow,
            NewClockOutAt: DateTimeOffset.UtcNow.AddHours(8),
            NewBreakMinutes: 30,
            Reason: "Some reason");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be("forbidden");
    }

    [Fact]
    public async Task Handle_shift_not_found_returns_404_not_found()
    {
        // Arrange
        var sut = CreateSut();
        var shiftId = Guid.NewGuid();

        _currentUser.Role.Returns(UserRole.Admin);
        _shiftEntryRepository.GetByIdAsync(shiftId, Arg.Any<CancellationToken>())
            .Returns((ShiftEntry?)null);

        var command = new AdminCorrectShiftCommand(
            ShiftId: shiftId,
            NewClockInAt: DateTimeOffset.UtcNow,
            NewClockOutAt: DateTimeOffset.UtcNow.AddHours(8),
            NewBreakMinutes: 30,
            Reason: "Legitimate reason");

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("shift_not_found");
    }
}
