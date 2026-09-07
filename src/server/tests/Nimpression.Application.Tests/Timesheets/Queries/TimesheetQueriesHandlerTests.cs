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

        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var shift = new ShiftEntry(shiftId, driver2Id, baseTime);

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
        var baseTime = new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.FromHours(12));
        var shift = new ShiftEntry(Guid.NewGuid(), driverId, baseTime);

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

    [Fact]
    public void ComputeSummary_MathematicalInvariant_AllDriversOrdinaryHours_Equals_SumOfIndividualDrivers()
    {
        // Arrange (W28 R1 核心不变量：全员正常工时 == 各司机分别查询的正常工时之和，全员加班工时 == 各司机加班工时之和)
        var driver1 = Guid.NewGuid();
        var driver2 = Guid.NewGuid();
        var driver3 = Guid.NewGuid();
        var drivers = new[] { driver1, driver2, driver3 };

        var fromDate = new DateOnly(2026, 8, 9);
        var toDate = new DateOnly(2026, 8, 22);
        var nzOffset = TimeSpan.FromHours(12);

        var shifts = new List<ShiftEntry>();

        // Day 1 (2026-08-09):
        // Driver 1: 9.5h raw - 30m break = 9.0h payable -> 8.0h ordinary, 1.0h overtime
        var s1 = new ShiftEntry(Guid.NewGuid(), driver1, new DateTimeOffset(2026, 8, 9, 8, 0, 0, nzOffset));
        s1.ClockOut(new DateTimeOffset(2026, 8, 9, 17, 30, 0, nzOffset), breakMinutes: 30);
        shifts.Add(s1);

        // Driver 2: 7.0h raw - 0m break = 7.0h payable -> 7.0h ordinary, 0.0h overtime
        var s2 = new ShiftEntry(Guid.NewGuid(), driver2, new DateTimeOffset(2026, 8, 9, 9, 0, 0, nzOffset));
        s2.ClockOut(new DateTimeOffset(2026, 8, 9, 16, 0, 0, nzOffset), breakMinutes: 0);
        shifts.Add(s2);

        // Driver 3: two split shifts on same day: 4.5h + 4.5h = 9.0h payable -> 8.0h ordinary, 1.0h overtime
        var s3a = new ShiftEntry(Guid.NewGuid(), driver3, new DateTimeOffset(2026, 8, 9, 6, 0, 0, nzOffset));
        s3a.ClockOut(new DateTimeOffset(2026, 8, 9, 10, 30, 0, nzOffset), breakMinutes: 0);
        var s3b = new ShiftEntry(Guid.NewGuid(), driver3, new DateTimeOffset(2026, 8, 9, 13, 0, 0, nzOffset));
        s3b.ClockOut(new DateTimeOffset(2026, 8, 9, 17, 30, 0, nzOffset), breakMinutes: 0);
        shifts.Add(s3a);
        shifts.Add(s3b);

        // Day 2 (2026-08-10):
        // Driver 1: 8.0h raw -> 8.0h ord, 0.0h ot
        var s4 = new ShiftEntry(Guid.NewGuid(), driver1, new DateTimeOffset(2026, 8, 10, 8, 0, 0, nzOffset));
        s4.ClockOut(new DateTimeOffset(2026, 8, 10, 16, 0, 0, nzOffset), breakMinutes: 0);
        shifts.Add(s4);

        // Driver 2: 10.0h raw -> 8.0h ord, 2.0h ot
        var s5 = new ShiftEntry(Guid.NewGuid(), driver2, new DateTimeOffset(2026, 8, 10, 7, 0, 0, nzOffset));
        s5.ClockOut(new DateTimeOffset(2026, 8, 10, 17, 0, 0, nzOffset), breakMinutes: 0);
        shifts.Add(s5);

        // Act: 1. 全员聚合
        var allSummary = ShiftEntryRepository.ComputeSummary(null, null, fromDate, toDate, shifts);

        // Act: 2. 各司机分别聚合
        var individualSummaries = drivers
            .Select(d => ShiftEntryRepository.ComputeSummary(d, $"Driver-{d}", fromDate, toDate, shifts))
            .ToList();

        // Assert: 数学恒等式 1：全员正常工时 == 各司机正常工时之和
        var sumOfIndividualOrdinary = individualSummaries.Sum(s => s.TotalOrdinaryHours);
        allSummary.TotalOrdinaryHours.Should().Be(sumOfIndividualOrdinary);

        // Assert: 数学恒等式 2：全员加班工时 == 各司机加班工时之和
        var sumOfIndividualOvertime = individualSummaries.Sum(s => s.TotalOvertimeHours);
        allSummary.TotalOvertimeHours.Should().Be(sumOfIndividualOvertime);

        // Assert: 数学恒等式 3：全员工时总和 == 各司机构成之和
        var sumOfIndividualPayable = individualSummaries.Sum(s => s.TotalPayableHours);
        allSummary.TotalPayableHours.Should().Be(sumOfIndividualPayable);
        allSummary.TotalPayableHours.Should().Be(allSummary.TotalOrdinaryHours + allSummary.TotalOvertimeHours);

        // Assert: 逐日明细恒等式
        foreach (var daily in allSummary.DailySummaries)
        {
            var dayOrdSum = individualSummaries.Sum(s =>
                s.DailySummaries.FirstOrDefault(d => d.Date == daily.Date)?.OrdinaryHours ?? 0m);
            var dayOtSum = individualSummaries.Sum(s =>
                s.DailySummaries.FirstOrDefault(d => d.Date == daily.Date)?.OvertimeHours ?? 0m);
            var dayPayableSum = individualSummaries.Sum(s =>
                s.DailySummaries.FirstOrDefault(d => d.Date == daily.Date)?.PayableHours ?? 0m);

            daily.OrdinaryHours.Should().Be(dayOrdSum);
            daily.OvertimeHours.Should().Be(dayOtSum);
            daily.PayableHours.Should().Be(dayPayableSum);
        }

        // 验证具体算术值：
        // Day 1: Driver 1 (8 ord, 1 ot) + Driver 2 (7 ord, 0 ot) + Driver 3 (8 ord, 1 ot) = 23.0 ord, 2.0 ot, 25.0 payable
        // Day 2: Driver 1 (8 ord, 0 ot) + Driver 2 (8 ord, 2 ot) + Driver 3 (0 ord, 0 ot) = 16.0 ord, 2.0 ot, 18.0 payable
        // Total: 39.0 ord, 4.0 ot, 43.0 payable
        allSummary.TotalOrdinaryHours.Should().Be(39.0m);
        allSummary.TotalOvertimeHours.Should().Be(4.0m);
        allSummary.TotalPayableHours.Should().Be(43.0m);
    }

    [Fact]
    public void ComputeSummary_SeededData_0908_to_2208_Period_Satisfies_Mathematical_Invariant()
    {
        // Arrange: 构造 10 名司机在 2026-08-09 至 2026-08-22 (14天) 的模拟班次
        var (users, drivers) = Nimpression.Infrastructure.Persistence.Seed.UserDriverSeeder.Generate();
        var (vehicles, _, _) = Nimpression.Infrastructure.Persistence.Seed.VehicleSeeder.Generate(drivers, users);
        var allShifts = Nimpression.Infrastructure.Persistence.Seed.TimesheetSeeder.Generate(drivers, vehicles, users, Nimpression.Infrastructure.Persistence.Seed.SeedConstants.DefaultSeed);

        var fromDate = new DateOnly(2026, 8, 9);
        var toDate = new DateOnly(2026, 8, 22);

        // Act: 1. 全员汇总
        var allSummary = ShiftEntryRepository.ComputeSummary(null, null, fromDate, toDate, allShifts);

        // Act: 2. 10 名司机各自独立汇总
        var driverSummaries = drivers
            .Select(d => ShiftEntryRepository.ComputeSummary(d.Id, d.EmployeeNo, fromDate, toDate, allShifts))
            .ToList();

        // Assert: 严格成立数学恒等式
        var sumOrd = driverSummaries.Sum(s => s.TotalOrdinaryHours);
        var sumOt = driverSummaries.Sum(s => s.TotalOvertimeHours);
        var sumPayable = driverSummaries.Sum(s => s.TotalPayableHours);

        allSummary.TotalOrdinaryHours.Should().Be(sumOrd);
        allSummary.TotalOvertimeHours.Should().Be(sumOt);
        allSummary.TotalPayableHours.Should().Be(sumPayable);

        // 验证 95 班次及修复后的精确数值：
        // 修复前全员 Ordinary 被 14*8h 压成 112.00h，Overtime 虚高为 611.75h；
        // 修复后全员 Ordinary 为 705.50h，Overtime 为 18.25h，二者之和严格等于总工时 723.75h。
        allSummary.TotalShifts.Should().Be(95);
        allSummary.TotalPayableHours.Should().Be(723.75m);
        allSummary.TotalOrdinaryHours.Should().Be(705.50m);
        allSummary.TotalOvertimeHours.Should().Be(18.25m);
        allSummary.TotalOrdinaryHours.Should().Be(sumOrd);
        allSummary.TotalOvertimeHours.Should().Be(sumOt);

        // 验证报告提及的 Liam (DRV-001) 与 Noah (DRV-002) 具体数值
        var liamSummary = driverSummaries.First(s => s.DriverId == drivers[0].Id);
        liamSummary.TotalOrdinaryHours.Should().Be(66.50m);
        liamSummary.TotalOvertimeHours.Should().Be(2.00m);
        liamSummary.TotalPayableHours.Should().Be(68.50m);

        var noahSummary = driverSummaries.First(s => s.DriverId == drivers[1].Id);
        noahSummary.TotalOrdinaryHours.Should().Be(79.50m);
        noahSummary.TotalOvertimeHours.Should().Be(1.50m);
        noahSummary.TotalPayableHours.Should().Be(81.00m);
    }

    [Fact]
    public void ComputeSummary_WithNullDateRange_AggregatesAllShifts_AndSetsEffectiveDates()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var nzOffset = TimeSpan.FromHours(12);

        var shift1 = new ShiftEntry(Guid.NewGuid(), driverId, new DateTimeOffset(2026, 7, 10, 8, 0, 0, nzOffset));
        shift1.ClockOut(new DateTimeOffset(2026, 7, 10, 16, 0, 0, nzOffset), breakMinutes: 0);

        var shift2 = new ShiftEntry(Guid.NewGuid(), driverId, new DateTimeOffset(2026, 8, 20, 8, 0, 0, nzOffset));
        shift2.ClockOut(new DateTimeOffset(2026, 8, 20, 17, 0, 0, nzOffset), breakMinutes: 0);

        var shifts = new List<ShiftEntry> { shift1, shift2 };

        // Act: passing null for both fromDate and toDate
        var summary = ShiftEntryRepository.ComputeSummary(driverId, "Test Driver", null, null, shifts);

        // Assert
        summary.TotalShifts.Should().Be(2);
        summary.TotalPayableHours.Should().Be(17.0m);
        summary.TotalOrdinaryHours.Should().Be(16.0m);
        summary.TotalOvertimeHours.Should().Be(1.0m);
        summary.FromDate.Should().Be(new DateOnly(2026, 7, 10));
        summary.ToDate.Should().Be(new DateOnly(2026, 8, 20));
        summary.DailySummaries.Should().HaveCount(2);
    }
}
