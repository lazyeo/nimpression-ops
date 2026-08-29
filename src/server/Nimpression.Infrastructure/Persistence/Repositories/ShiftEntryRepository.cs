using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Timesheets.Abstractions;
using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;

namespace Nimpression.Infrastructure.Persistence.Repositories;

/// <summary>
/// 班次打卡与工时仓储实现。
/// 封装 EF Core 查询与统一工时聚合计算逻辑（保证司机端与管理端口径 0 误差）。
/// </summary>
public sealed class ShiftEntryRepository(AppDbContext dbContext) : IShiftEntryRepository
{
    public async Task<ShiftEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.ShiftEntries
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<ShiftEntry?> GetActiveShiftByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ShiftEntries
            .FirstOrDefaultAsync(s => s.DriverId == driverId && s.Status == ShiftStatus.Active, cancellationToken);
    }

    public async Task<bool> HasActiveShiftAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ShiftEntries
            .AnyAsync(s => s.DriverId == driverId && s.Status == ShiftStatus.Active, cancellationToken);
    }

    public async Task<Guid?> GetDriverIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers
            .Where(d => d.UserId == userId)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers
            .AnyAsync(d => d.Id == driverId, cancellationToken);
    }

    public async Task AddAsync(ShiftEntry shift, CancellationToken cancellationToken = default)
    {
        await dbContext.ShiftEntries.AddAsync(shift, cancellationToken);
    }

    public void Update(ShiftEntry shift)
    {
        dbContext.ShiftEntries.Update(shift);
    }

    public async Task<PagedResult<ShiftEntryDto>> GetShiftsPagedAsync(
        TimesheetFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ShiftEntries.AsNoTracking().AsQueryable();

        if (filter.DriverId.HasValue)
        {
            query = query.Where(s => s.DriverId == filter.DriverId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.Status == filter.Status.Value);
        }

        if (filter.FromDate.HasValue)
        {
            var fromDate = filter.FromDate.Value;
            var fromLocal = fromDate.ToDateTime(TimeOnly.MinValue);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, NzTimeZone.Info);
            query = query.Where(s => s.ClockInAt >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toDate = filter.ToDate.Value;
            var toLocal = toDate.ToDateTime(TimeOnly.MaxValue);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, NzTimeZone.Info);
            query = query.Where(s => s.ClockInAt <= toUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;

        var shifts = await query
            .OrderByDescending(s => s.ClockInAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 获取相关司机名称
        var driverIds = shifts.Select(s => s.DriverId).Distinct().ToList();
        var driverNames = await dbContext.Drivers
            .AsNoTracking()
            .Where(d => driverIds.Contains(d.Id))
            .Join(dbContext.Users.AsNoTracking(), d => d.UserId, u => u.Id, (d, u) => new { d.Id, u.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var items = shifts.Select(s =>
        {
            driverNames.TryGetValue(s.DriverId, out var driverName);
            return ShiftEntryDto.FromEntity(s, driverName);
        }).ToList();

        return new PagedResult<ShiftEntryDto>(items, totalCount, page, pageSize);
    }

    public async Task<TimesheetSummaryDto> GetSummaryAsync(
        TimesheetSummaryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var fromDate = filter.FromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-13));
        var toDate = filter.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        var fromLocal = fromDate.ToDateTime(TimeOnly.MinValue);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, NzTimeZone.Info);

        var toLocal = toDate.ToDateTime(TimeOnly.MaxValue);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, NzTimeZone.Info);

        var query = dbContext.ShiftEntries
            .AsNoTracking()
            .Where(s => s.ClockInAt >= fromUtc && s.ClockInAt <= toUtc);

        if (filter.DriverId.HasValue)
        {
            query = query.Where(s => s.DriverId == filter.DriverId.Value);
        }

        var shifts = await query.ToListAsync(cancellationToken);

        string? driverName = null;
        if (filter.DriverId.HasValue)
        {
            driverName = await dbContext.Drivers
                .AsNoTracking()
                .Where(d => d.Id == filter.DriverId.Value)
                .Join(dbContext.Users.AsNoTracking(), d => d.UserId, u => u.Id, (_, u) => u.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return ComputeSummary(filter.DriverId, driverName, fromDate, toDate, shifts);
    }

    /// <summary>
    /// 统一工时汇总计算逻辑（纯逻辑，无 IO）。
    /// 司机端与管理端完全共享此聚合算法，确保两端数字完全一致、误差为 0。
    /// </summary>
    public static TimesheetSummaryDto ComputeSummary(
        Guid? driverId,
        string? driverName,
        DateOnly fromDate,
        DateOnly toDate,
        IEnumerable<ShiftEntry> shifts)
    {
        var completedCalculatedShifts = shifts
            .Where(s => s.Status == ShiftStatus.Completed && s.ClockOutAt.HasValue)
            .Select(s =>
            {
                var duration = ShiftDurationCalculator.Calculate(s);
                return new
                {
                    Shift = s,
                    Duration = duration
                };
            })
            .Where(x => x.Duration.AttributedDate >= fromDate && x.Duration.AttributedDate <= toDate)
            .ToList();

        var dailyGroups = completedCalculatedShifts
            .GroupBy(x => x.Duration.AttributedDate)
            .OrderBy(g => g.Key)
            .ToList();

        var dailySummaries = new List<TimesheetDailySummaryDto>();
        decimal totalOrdinaryHours = 0m;
        decimal totalOvertimeHours = 0m;
        decimal totalPayableHours = 0m;
        int totalBreakMinutes = 0;
        int totalShifts = completedCalculatedShifts.Count;

        foreach (var group in dailyGroups)
        {
            var date = group.Key;
            var shiftCount = group.Count();
            var dayPayableHours = group.Sum(x => x.Duration.PayableHours.Value);
            var dayBreakMinutes = group.Sum(x => x.Shift.BreakMinutes);

            var ordinary = Math.Min(8.00m, dayPayableHours);
            var overtime = Math.Max(0m, dayPayableHours - 8.00m);

            totalOrdinaryHours += ordinary;
            totalOvertimeHours += overtime;
            totalPayableHours += dayPayableHours;
            totalBreakMinutes += dayBreakMinutes;

            dailySummaries.Add(new TimesheetDailySummaryDto(
                Date: date,
                ShiftCount: shiftCount,
                PayableHours: dayPayableHours,
                OrdinaryHours: ordinary,
                OvertimeHours: overtime,
                BreakMinutes: dayBreakMinutes));
        }

        return new TimesheetSummaryDto(
            DriverId: driverId,
            DriverName: driverName,
            FromDate: fromDate,
            ToDate: toDate,
            TotalShifts: totalShifts,
            TotalPayableHours: totalPayableHours,
            TotalOrdinaryHours: totalOrdinaryHours,
            TotalOvertimeHours: totalOvertimeHours,
            TotalBreakMinutes: totalBreakMinutes,
            DailySummaries: dailySummaries);
    }
}
