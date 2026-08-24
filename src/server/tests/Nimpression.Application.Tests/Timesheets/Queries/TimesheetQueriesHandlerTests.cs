using FluentAssertions;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Application.Features.Timesheets.Queries.GetCurrentActiveShift;
using Nimpression.Application.Features.Timesheets.Queries.GetShiftById;
using Nimpression.Application.Features.Timesheets.Queries.GetTimesheetsList;
using Nimpression.Application.Features.Timesheets.Queries.GetTimesheetSummary;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Infrastructure.Persistence.Repositories;
using NSubstitute;
using Xunit;

namespace Nimpression.Application.Tests.Timesheets.Queries;

public sealed class TimesheetQueriesHandlerTests
{
    private readonly IShiftEntryRepository _shiftEntryRepository = Substitute.For<IShiftEntryRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    [Fact]
    public async Task GetShiftById_driver_queries_own_shift_returns_dto_with_calculated_duration()
    {
        // Arrange (F6.2: 跨零点 22:00 -> 06:00 归属上班日)
        var shiftId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var clockIn = new DateTimeOffset(2026, 8, 20, 22, 0, 0, TimeSpan.FromHours(12));
        var clockOut = new DateTimeOffset(2026, 8, 21, 6, 0, 0, TimeSpan.FromHours(12));
        var shift = new ShiftEntry(shiftId, driverId, clockIn);
        shift.ClockOut(clockOut, breakMinutes: 0);

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetByIdAsync(shiftId, Arg.Any<CancellationToken>()).Returns(shift);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(driverId);

        var handler = new GetShiftByIdQueryHandler(_shiftEntryRepository, _currentUser);

        // Act
        var result = await handler.Handle(new GetShiftByIdQuery(shiftId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(shiftId);
        result.Value.AttributedDate.Should().Be(new DateOnly(2026, 8, 20));
        result.Value.RawDurationHours.Should().Be(8.0m);
        result.Value.PayableHours.Should().Be(8.0m);
    }

    [Fact]
    public async Task GetShiftById_driver_queries_other_driver_shift_returns_403_forbidden()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var driver1Id = Guid.NewGuid();
        var driver2Id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var shift = new ShiftEntry(shiftId, driver2Id, DateTimeOffset.UtcNow);

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetByIdAsync(shiftId, Arg.Any<CancellationToken>()).Returns(shift);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(driver1Id);

        var handler = new GetShiftByIdQueryHandler(_shiftEntryRepository, _currentUser);

        // Act
        var result = await handler.Handle(new GetShiftByIdQuery(shiftId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
    }

    [Fact]
    public async Task GetCurrentActiveShift_returns_active_shift_or_null()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shift = new ShiftEntry(Guid.NewGuid(), driverId, DateTimeOffset.UtcNow);

        _currentUser.Role.Returns(UserRole.Driver);
        _currentUser.UserId.Returns(userId);
        _shiftEntryRepository.GetDriverIdByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(driverId);
        _shiftEntryRepository.GetActiveShiftByDriverIdAsync(driverId, Arg.Any<CancellationToken>()).Returns(shift);

        var handler = new GetCurrentActiveShiftQueryHandler(_shiftEntryRepository, _currentUser);

        // Act
        var result = await handler.Handle(new GetCurrentActiveShiftQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DriverId.Should().Be(driverId);
        result.Value.Status.Should().Be(ShiftStatus.Active);
    }

    [Fact]
    public async Task GetTimesheetSummary_computes_identical_numbers_for_both_perspectives()
    {
        // Arrange (F6.5: 验证同一批班次在 ComputeSummary 计算下输出完全一致)
        var driverId = Guid.NewGuid();
        var fromDate = new DateOnly(2026, 8, 17);
        var toDate = new DateOnly(2026, 8, 23);

        var nzOffset = TimeSpan.FromHours(12);

        // 班次 1: 8月17日 白班 08:00 - 17:00 (9h raw, 30m break = 8.5h payable -> 8.0h ord, 0.5h ot)
        var shift1 = new ShiftEntry(Guid.NewGuid(), driverId, new DateTimeOffset(2026, 8, 17, 8, 0, 0, nzOffset));
        shift1.ClockOut(new DateTimeOffset(2026, 8, 17, 17, 0, 0, nzOffset), breakMinutes: 30);

        // 班次 2: 8月18日 跨零点夜班 22:00 - 次日 06:00 (8h raw, 0 break = 8.0h payable -> 8.0h ord, 0 ot) -> 归属 8月18日
        var shift2 = new ShiftEntry(Guid.NewGuid(), driverId, new DateTimeOffset(2026, 8, 18, 22, 0, 0, nzOffset));
        shift2.ClockOut(new DateTimeOffset(2026, 8, 19, 6, 0, 0, nzOffset), breakMinutes: 0);

        var shifts = new List<ShiftEntry> { shift1, shift2 };

        // Act
        var summary = ShiftEntryRepository.ComputeSummary(driverId, "Driver Dave", fromDate, toDate, shifts);

        // Assert
        summary.TotalShifts.Should().Be(2);
        summary.TotalPayableHours.Should().Be(16.5m);
        summary.TotalOrdinaryHours.Should().Be(16.0m);
        summary.TotalOvertimeHours.Should().Be(0.5m);
        summary.TotalBreakMinutes.Should().Be(30);

        summary.DailySummaries.Should().HaveCount(2);

        var day1 = summary.DailySummaries.First(d => d.Date == new DateOnly(2026, 8, 17));
        day1.PayableHours.Should().Be(8.5m);
        day1.OrdinaryHours.Should().Be(8.0m);
        day1.OvertimeHours.Should().Be(0.5m);

        var day2 = summary.DailySummaries.First(d => d.Date == new DateOnly(2026, 8, 18));
        day2.PayableHours.Should().Be(8.0m);
        day2.OrdinaryHours.Should().Be(8.0m);
        day2.OvertimeHours.Should().Be(0m);
    }
}
