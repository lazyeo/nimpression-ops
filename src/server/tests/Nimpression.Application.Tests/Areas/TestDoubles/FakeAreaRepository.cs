using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Application.Features.Areas.DTOs;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Tests.Areas.TestDoubles;

public sealed class FakeAreaRepository : IAreaRepository
{
    public Dictionary<Guid, Area> Areas { get; } = [];
    public Dictionary<Guid, AreaAssignment> Assignments { get; } = [];
    public HashSet<Guid> ExistingDriverIds { get; } = [];
    public bool ThrowOnAddArea { get; set; }
    public Exception? ExceptionToThrowOnAddArea { get; set; }
    public bool ThrowOnUpdateArea { get; set; }
    public Exception? ExceptionToThrowOnUpdateArea { get; set; }
    public bool ThrowOnDeleteArea { get; set; }
    public Exception? ExceptionToThrowOnDeleteArea { get; set; }

    public Task<Area?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Areas.TryGetValue(id, out var area);
        return Task.FromResult(area);
    }

    public Task<Area?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var area = Areas.Values.FirstOrDefault(a => string.Equals(a.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(area);
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var exists = Areas.Values.Any(a => string.Equals(a.Code, code.Trim(), StringComparison.OrdinalIgnoreCase) && (!excludeId.HasValue || a.Id != excludeId.Value));
        return Task.FromResult(exists);
    }

    public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingDriverIds.Contains(driverId));
    }

    public Task<bool> HasActiveAssignmentsAsync(Guid areaId, DateOnly referenceDate, CancellationToken cancellationToken = default)
    {
        var hasActive = Assignments.Values.Any(aa => aa.AreaId == areaId && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate));
        return Task.FromResult(hasActive);
    }

    public Task AddAreaAsync(Area area, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAddArea && ExceptionToThrowOnAddArea != null)
        {
            throw ExceptionToThrowOnAddArea;
        }

        Areas[area.Id] = area;
        return Task.CompletedTask;
    }

    public void UpdateArea(Area area)
    {
        if (ThrowOnUpdateArea && ExceptionToThrowOnUpdateArea != null)
        {
            throw ExceptionToThrowOnUpdateArea;
        }

        Areas[area.Id] = area;
    }

    public void DeleteArea(Area area)
    {
        if (ThrowOnDeleteArea && ExceptionToThrowOnDeleteArea != null)
        {
            throw ExceptionToThrowOnDeleteArea;
        }

        Areas.Remove(area.Id);
    }

    public Task<PagedResult<AreaDto>> GetAreasPagedAsync(AreaFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Areas.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            query = query.Where(a => a.Name.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                                     a.Code.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(a => a.IsActive == filter.IsActive.Value);
        }

        var total = query.Count();
        var items = query
            .OrderBy(a => a.Code)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => new AreaDto(a.Id, a.Name, a.Code, a.Description, a.GeoJson, a.IsActive))
            .ToList();

        return Task.FromResult(new PagedResult<AreaDto>(items, total, filter.Page, filter.PageSize));
    }

    public Task<AreaDetailDto?> GetAreaDetailByIdAsync(Guid id, DateOnly referenceDate, CancellationToken cancellationToken = default)
    {
        if (!Areas.TryGetValue(id, out var a))
        {
            return Task.FromResult<AreaDetailDto?>(null);
        }

        var activeDriversCount = Assignments.Values.Count(aa => aa.AreaId == id && aa.EffectiveFrom <= referenceDate && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate));
        var dto = new AreaDetailDto(a.Id, a.Name, a.Code, a.Description, a.GeoJson, a.IsActive, activeDriversCount);
        return Task.FromResult<AreaDetailDto?>(dto);
    }

    public Task<List<AreaAssignment>> GetAssignmentsForDriverAndAreaAsync(Guid driverId, Guid areaId, CancellationToken cancellationToken = default)
    {
        var list = Assignments.Values
            .Where(aa => aa.DriverId == driverId && aa.AreaId == areaId)
            .ToList();

        return Task.FromResult(list);
    }

    public Task<AreaAssignment?> GetAssignmentByIdAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        Assignments.TryGetValue(assignmentId, out var a);
        return Task.FromResult(a);
    }

    public Task AddAssignmentAsync(AreaAssignment assignment, CancellationToken cancellationToken = default)
    {
        Assignments[assignment.Id] = assignment;
        return Task.CompletedTask;
    }

    public void DeleteAssignment(AreaAssignment assignment)
    {
        Assignments.Remove(assignment.Id);
    }

    public Task<List<AreaAssignmentDto>> GetAreaAssignmentsAsync(Guid? areaId, Guid? driverId, DateOnly referenceDate, CancellationToken cancellationToken = default)
    {
        var query = Assignments.Values.AsEnumerable();
        if (areaId.HasValue) query = query.Where(aa => aa.AreaId == areaId.Value);
        if (driverId.HasValue) query = query.Where(aa => aa.DriverId == driverId.Value);

        var list = query
            .OrderByDescending(aa => aa.EffectiveFrom)
            .Select(aa =>
            {
                var areaName = Areas.TryGetValue(aa.AreaId, out var a) ? a.Name : "Area";
                var areaCode = Areas.TryGetValue(aa.AreaId, out a) ? a.Code : "CODE";
                var isActive = aa.EffectiveFrom <= referenceDate && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate);
                return new AreaAssignmentDto(aa.Id, aa.AreaId, areaName, areaCode, aa.DriverId, aa.EffectiveFrom, aa.EffectiveTo, isActive);
            })
            .ToList();

        return Task.FromResult(list);
    }

    public Task<bool> IsDriverAssignedToAreaOnDateAsync(Guid driverId, Guid areaId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var assigned = Assignments.Values.Any(aa => aa.DriverId == driverId && aa.AreaId == areaId && aa.EffectiveFrom <= date && (aa.EffectiveTo == null || aa.EffectiveTo >= date));
        return Task.FromResult(assigned);
    }
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }
    public bool ThrowOnSave { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave && ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        SaveChangesCallCount++;
        return Task.FromResult(1);
    }

    public Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IAsyncDisposable>(new NoOpAsyncDisposable());
    }

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class FakeDateTimeProvider(DateTimeOffset? fixedUtcNow = null) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = fixedUtcNow ?? new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
    public DateTimeOffset NzNow => UtcNow.ToOffset(TimeSpan.FromHours(12));
    public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
}
