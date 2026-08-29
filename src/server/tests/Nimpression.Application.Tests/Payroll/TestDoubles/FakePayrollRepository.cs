using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Payroll.Abstractions;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Common;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;

namespace Nimpression.Application.Tests.Payroll.TestDoubles;

public sealed class FakePayrollRepository : IPayrollRepository
{
    public Dictionary<Guid, PayPeriod> PayPeriods { get; } = [];
    public Dictionary<Guid, Payslip> Payslips { get; } = [];
    public Dictionary<Guid, Driver> Drivers { get; } = [];
    public List<ShiftEntry> Shifts { get; } = [];
    public List<JobTask> Tasks { get; } = [];
    public List<Fine> Fines { get; } = [];

    public Task<PayPeriod?> GetPayPeriodByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        PayPeriods.TryGetValue(id, out var period);
        return Task.FromResult(period);
    }

    public Task<PagedResult<PayPeriodDto>> GetPayPeriodsListAsync(PayPeriodFilter filter, CancellationToken cancellationToken = default)
    {
        var query = PayPeriods.Values.AsEnumerable();

        if (filter.Status.HasValue) query = query.Where(p => p.Status == filter.Status.Value);
        if (filter.FromDate.HasValue) query = query.Where(p => p.StartsOn >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(p => p.EndsOn <= filter.ToDate.Value);

        var total = query.Count();
        var items = query
            .OrderByDescending(p => p.StartsOn)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => PayPeriodDto.FromEntity(p, Payslips.Values.Count(ps => ps.PayPeriodId == p.Id)))
            .ToList();

        return Task.FromResult(new PagedResult<PayPeriodDto>(items, total, filter.Page, filter.PageSize));
    }

    public Task<bool> HasOverlappingPayPeriodAsync(DateOnly startsOn, DateOnly endsOn, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var hasOverlap = PayPeriods.Values.Any(p =>
            (!excludeId.HasValue || p.Id != excludeId.Value) &&
            startsOn <= p.EndsOn && p.StartsOn <= endsOn);

        return Task.FromResult(hasOverlap);
    }

    public Task AddPayPeriodAsync(PayPeriod payPeriod, CancellationToken cancellationToken = default)
    {
        PayPeriods[payPeriod.Id] = payPeriod;
        return Task.CompletedTask;
    }

    public void UpdatePayPeriod(PayPeriod payPeriod)
    {
        PayPeriods[payPeriod.Id] = payPeriod;
    }

    public Task<Payslip?> GetPayslipByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Payslips.TryGetValue(id, out var payslip);
        return Task.FromResult(payslip);
    }

    public Task<Payslip?> GetPayslipByPeriodAndDriverAsync(Guid payPeriodId, Guid driverId, CancellationToken cancellationToken = default)
    {
        var payslip = Payslips.Values.FirstOrDefault(p => p.PayPeriodId == payPeriodId && p.DriverId == driverId);
        return Task.FromResult(payslip);
    }

    public Task<IReadOnlyList<Payslip>> GetPayslipsByPeriodIdAsync(Guid payPeriodId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Payslip> list = Payslips.Values.Where(p => p.PayPeriodId == payPeriodId).ToList();
        return Task.FromResult(list);
    }

    public Task<PagedResult<PayslipDto>> GetPayslipsForDriverPagedAsync(DriverPayslipsFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Payslips.Values.Where(p => p.DriverId == filter.DriverId);

        Drivers.TryGetValue(filter.DriverId, out var driver);

        var total = query.Count();
        var items = query
            .OrderByDescending(p => p.CalculatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p =>
            {
                PayPeriods.TryGetValue(p.PayPeriodId, out var period);
                return PayslipDto.FromEntity(
                    payslip: p,
                    startsOn: period?.StartsOn ?? DateOnly.MinValue,
                    endsOn: period?.EndsOn ?? DateOnly.MaxValue,
                    driverName: null,
                    employeeNo: driver?.EmployeeNo);
            })
            .ToList();

        return Task.FromResult(new PagedResult<PayslipDto>(items, total, filter.Page, filter.PageSize));
    }

    public Task AddPayslipAsync(Payslip payslip, CancellationToken cancellationToken = default)
    {
        Payslips[payslip.Id] = payslip;
        return Task.CompletedTask;
    }

    public void RemovePayslip(Payslip payslip)
    {
        Payslips.Remove(payslip.Id);
    }

    public void RemovePayslips(IEnumerable<Payslip> payslips)
    {
        foreach (var p in payslips)
        {
            Payslips.Remove(p.Id);
        }
    }

    public Task<Driver?> GetDriverByIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        Drivers.TryGetValue(driverId, out var driver);
        return Task.FromResult(driver);
    }

    public Task<Driver?> GetDriverByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var driver = Drivers.Values.FirstOrDefault(d => d.UserId == userId);
        return Task.FromResult(driver);
    }

    public Task<IReadOnlyList<Driver>> GetActiveDriversAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Driver> list = Drivers.Values.Where(d => d.Status == DriverStatus.Active).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<ShiftEntry>> GetCompletedShiftsForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ShiftEntry> list = Shifts
            .Where(s => s.DriverId == driverId && s.ClockOutAt.HasValue)
            .Where(s =>
            {
                var dur = ShiftDurationCalculator.Calculate(s);
                return dur.AttributedDate >= startsOn && dur.AttributedDate <= endsOn;
            })
            .ToList();

        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<JobTask>> GetCompletedJobTasksForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<JobTask> list = Tasks
            .Where(t => t.DriverId == driverId && t.Status == JobTaskStatus.Completed && t.CompletedAt.HasValue)
            .Where(t =>
            {
                var date = NzTimeZone.ToNzDateOnly(t.CompletedAt!.Value);
                return date >= startsOn && date <= endsOn;
            })
            .ToList();

        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<Fine>> GetFinesForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Fine> list = Fines
            .Where(f => f.DriverId == driverId && f.IssuedOn >= startsOn && f.IssuedOn <= endsOn)
            .ToList();

        return Task.FromResult(list);
    }
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }

    public Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IAsyncDisposable>(new NoOpAsyncDisposable());

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class FakeCurrentUser(
    Guid? userId = null,
    UserRole? role = UserRole.Admin,
    string? ipAddress = "127.0.0.1",
    string? userAgent = "TestAgent") : ICurrentUser
{
    public Guid? UserId { get; set; } = userId ?? Guid.NewGuid();
    public UserRole? Role { get; set; } = role;
    public string? IpAddress { get; set; } = ipAddress;
    public string? UserAgent { get; set; } = userAgent;
    public bool IsAuthenticated => UserId.HasValue;
}

public sealed class FakeAuditSink : IAuditSink
{
    public List<(string EntityType, Guid? EntityId, string Action, string? BeforeJson, string? AfterJson)> RecordedAudits { get; } = [];

    public Task RecordAsync(
        string entityType,
        Guid? entityId,
        string action,
        string? beforeJson = null,
        string? afterJson = null,
        CancellationToken cancellationToken = default)
    {
        RecordedAudits.Add((entityType, entityId, action, beforeJson, afterJson));
        return Task.CompletedTask;
    }
}

public sealed class FakeDateTimeProvider(DateTimeOffset? fixedUtcNow = null) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = fixedUtcNow ?? new DateTimeOffset(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);
    public DateTimeOffset NzNow => UtcNow.ToOffset(TimeSpan.FromHours(12));
    public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
}
