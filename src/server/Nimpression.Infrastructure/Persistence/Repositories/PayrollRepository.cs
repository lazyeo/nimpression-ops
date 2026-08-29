using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Payroll.Abstractions;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;

namespace Nimpression.Infrastructure.Persistence.Repositories;

/// <summary>
/// 薪资模块 EF Core 仓储实现。
/// </summary>
public sealed class PayrollRepository(AppDbContext dbContext) : IPayrollRepository
{
    public async Task<PayPeriod?> GetPayPeriodByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.PayPeriods
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PagedResult<PayPeriodDto>> GetPayPeriodsListAsync(
        PayPeriodFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PayPeriods.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(p => p.Status == filter.Status.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(p => p.StartsOn >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(p => p.EndsOn <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;

        var periods = await query
            .OrderByDescending(p => p.StartsOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var periodIds = periods.Select(p => p.Id).ToList();
        var counts = await dbContext.Payslips.AsNoTracking()
            .Where(p => periodIds.Contains(p.PayPeriodId))
            .GroupBy(p => p.PayPeriodId)
            .Select(g => new { PeriodId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PeriodId, x => x.Count, cancellationToken);

        var dtos = periods.Select(p =>
        {
            counts.TryGetValue(p.Id, out var count);
            return PayPeriodDto.FromEntity(p, count);
        }).ToList();

        return new PagedResult<PayPeriodDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<bool> HasOverlappingPayPeriodAsync(
        DateOnly startsOn,
        DateOnly endsOn,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PayPeriods.AsNoTracking().AsQueryable();

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        // 两个区间 [s1, e1] 与 [s2, e2] 重叠当且仅当 s1 <= e2 && s2 <= e1
        return await query.AnyAsync(p => startsOn <= p.EndsOn && p.StartsOn <= endsOn, cancellationToken);
    }

    public async Task AddPayPeriodAsync(PayPeriod payPeriod, CancellationToken cancellationToken = default)
    {
        await dbContext.PayPeriods.AddAsync(payPeriod, cancellationToken);
    }

    public void UpdatePayPeriod(PayPeriod payPeriod)
    {
        dbContext.PayPeriods.Update(payPeriod);
    }

    public async Task<Payslip?> GetPayslipByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payslips
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Payslip?> GetPayslipByPeriodAndDriverAsync(
        Guid payPeriodId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Payslips
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.PayPeriodId == payPeriodId && p.DriverId == driverId, cancellationToken);
    }

    public async Task<IReadOnlyList<Payslip>> GetPayslipsByPeriodIdAsync(
        Guid payPeriodId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Payslips
            .Include(p => p.Lines)
            .Where(p => p.PayPeriodId == payPeriodId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<PayslipDto>> GetPayslipsForDriverPagedAsync(
        DriverPayslipsFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Payslips
            .AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.DriverId == filter.DriverId);

        var totalCount = await query.CountAsync(cancellationToken);
        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;

        var payslips = await query
            .OrderByDescending(p => p.CalculatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var periodIds = payslips.Select(p => p.PayPeriodId).Distinct().ToList();
        var periods = await dbContext.PayPeriods.AsNoTracking()
            .Where(p => periodIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var driver = await dbContext.Drivers.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == filter.DriverId, cancellationToken);

        var dtos = payslips.Select(p =>
        {
            periods.TryGetValue(p.PayPeriodId, out var period);
            return PayslipDto.FromEntity(
                payslip: p,
                startsOn: period?.StartsOn ?? DateOnly.MinValue,
                endsOn: period?.EndsOn ?? DateOnly.MaxValue,
                driverName: null,
                employeeNo: driver?.EmployeeNo);
        }).ToList();

        return new PagedResult<PayslipDto>(dtos, totalCount, page, pageSize);
    }

    public async Task AddPayslipAsync(Payslip payslip, CancellationToken cancellationToken = default)
    {
        await dbContext.Payslips.AddAsync(payslip, cancellationToken);
    }

    public void RemovePayslip(Payslip payslip)
    {
        dbContext.Payslips.Remove(payslip);
    }

    public void RemovePayslips(IEnumerable<Payslip> payslips)
    {
        dbContext.Payslips.RemoveRange(payslips);
    }

    public async Task<Driver?> GetDriverByIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);
    }

    public async Task<Driver?> GetDriverByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<Driver>> GetActiveDriversAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers
            .Where(d => d.Status == DriverStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftEntry>> GetCompletedShiftsForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default)
    {
        // 扩展查询时间窗口（考虑 NZ 时区与跨零点班次），在内存中精确根据 AttributedDate 筛选
        var fromLocal = startsOn.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var toLocal = endsOn.AddDays(1).ToDateTime(TimeOnly.MaxValue);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, NzTimeZone.Info);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, NzTimeZone.Info);

        return await dbContext.ShiftEntries
            .Where(s => s.DriverId == driverId &&
                        s.ClockInAt >= fromUtc &&
                        s.ClockInAt <= toUtc &&
                        s.ClockOutAt.HasValue)
            .OrderBy(s => s.ClockInAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobTask>> GetCompletedJobTasksForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default)
    {
        var fromLocal = startsOn.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var toLocal = endsOn.AddDays(1).ToDateTime(TimeOnly.MaxValue);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, NzTimeZone.Info);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, NzTimeZone.Info);

        return await dbContext.JobTasks
            .Where(t => t.DriverId == driverId &&
                        t.CompletedAt.HasValue &&
                        t.CompletedAt.Value >= fromUtc &&
                        t.CompletedAt.Value <= toUtc)
            .OrderBy(t => t.CompletedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Fine>> GetFinesForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Fines
            .AsNoTracking()
            .Where(f => f.DriverId == driverId && f.IssuedOn >= startsOn && f.IssuedOn <= endsOn)
            .OrderBy(f => f.IssuedOn)
            .ToListAsync(cancellationToken);
    }
}
