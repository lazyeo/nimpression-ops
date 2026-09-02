using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.DTOs;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Tests.Vehicles.TestDoubles;

public sealed class FakeVehicleRepository : IVehicleRepository
{
    public Dictionary<Guid, Vehicle> Vehicles { get; } = [];
    public Dictionary<Guid, VehicleAssignment> Assignments { get; } = [];
    public List<OdometerReading> OdometerReadings { get; } = [];
    public HashSet<Guid> ExistingDriverIds { get; } = [];
    public bool ThrowOnAddAssignment { get; set; }
    public Exception? ExceptionToThrowOnAddAssignment { get; set; }
    public bool ThrowOnAddVehicle { get; set; }
    public Exception? ExceptionToThrowOnAddVehicle { get; set; }

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Vehicles.TryGetValue(id, out var vehicle);
        return Task.FromResult(vehicle);
    }

    public Task<Vehicle?> GetByRegoAsync(Rego rego, CancellationToken cancellationToken = default)
    {
        var vehicle = Vehicles.Values.FirstOrDefault(v => v.Rego == rego);
        return Task.FromResult(vehicle);
    }

    public Task<bool> ExistsByRegoAsync(Rego rego, CancellationToken cancellationToken = default)
    {
        var exists = Vehicles.Values.Any(v => v.Rego == rego);
        return Task.FromResult(exists);
    }

    public Task<PagedResult<VehicleSummaryDto>> GetVehiclesPagedAsync(VehicleFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Vehicles.Values.AsEnumerable();
        if (filter.Status.HasValue)
        {
            query = query.Where(v => v.Status == filter.Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(v => v.Rego.Value.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ||
                                     v.Make.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ||
                                     v.Model.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
        }

        var total = query.Count();
        var items = query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(v => new VehicleSummaryDto(
                v.Id,
                v.Rego.Value,
                v.Make,
                v.Model,
                v.Year,
                v.OdometerKm.Value,
                v.ServiceIntervalKm.Value,
                v.LastServiceOdometerKm.Value,
                v.DistanceSinceLastService.Value,
                v.IsServiceDue,
                v.WofExpiry,
                v.CofExpiry,
                v.InsuranceExpiry,
                v.Status,
                null,
                null))
            .ToList();

        return Task.FromResult(new PagedResult<VehicleSummaryDto>(items, total, filter.Page, filter.PageSize));
    }

    public Task<VehicleDetailDto?> GetVehicleDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!Vehicles.TryGetValue(id, out var v))
        {
            return Task.FromResult<VehicleDetailDto?>(null);
        }

        var activeAssignment = Assignments.Values.FirstOrDefault(a => a.VehicleId == id && a.IsActive);
        VehicleAssignmentDto? assignmentDto = activeAssignment is null ? null : new VehicleAssignmentDto(
            activeAssignment.Id,
            activeAssignment.VehicleId,
            v.Rego.Value,
            activeAssignment.DriverId,
            "Driver Name",
            "EMP-001",
            activeAssignment.AssignedAt,
            activeAssignment.ReleasedAt,
            activeAssignment.AssignedByUserId,
            "Dispatcher Name",
            activeAssignment.IsActive);

        var dto = new VehicleDetailDto(
            v.Id,
            v.Rego.Value,
            v.Make,
            v.Model,
            v.Year,
            v.VinEnc,
            v.OdometerKm.Value,
            v.ServiceIntervalKm.Value,
            v.LastServiceOdometerKm.Value,
            v.DistanceSinceLastService.Value,
            v.IsServiceDue,
            v.WofExpiry,
            v.CofExpiry,
            v.InsuranceExpiry,
            v.Status,
            assignmentDto,
            null);

        return Task.FromResult<VehicleDetailDto?>(dto);
    }

    public Task AddVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAddVehicle && ExceptionToThrowOnAddVehicle != null)
        {
            throw ExceptionToThrowOnAddVehicle;
        }

        Vehicles[vehicle.Id] = vehicle;
        return Task.CompletedTask;
    }

    public void UpdateVehicle(Vehicle vehicle)
    {
        Vehicles[vehicle.Id] = vehicle;
    }

    public Dictionary<Guid, Guid> DriverUserIdToDriverId { get; } = [];

    public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingDriverIds.Contains(driverId));
    }

    public Task<Guid?> GetDriverIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (DriverUserIdToDriverId.TryGetValue(userId, out var driverId))
        {
            return Task.FromResult<Guid?>(driverId);
        }
        return Task.FromResult<Guid?>(null);
    }

    public Task<VehicleAssignment?> GetAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Assignments.TryGetValue(id, out var a);
        return Task.FromResult(a);
    }

    public Task<VehicleAssignment?> GetActiveAssignmentByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var a = Assignments.Values.FirstOrDefault(x => x.VehicleId == vehicleId && x.IsActive);
        return Task.FromResult(a);
    }

    public Task<IReadOnlyList<VehicleAssignmentDto>> GetAssignmentsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var list = Assignments.Values
            .Where(a => a.VehicleId == vehicleId)
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new VehicleAssignmentDto(
                a.Id,
                a.VehicleId,
                Vehicles.TryGetValue(a.VehicleId, out var v) ? v.Rego.Value : "REGO",
                a.DriverId,
                "Driver Name",
                "EMP-001",
                a.AssignedAt,
                a.ReleasedAt,
                a.AssignedByUserId,
                "Dispatcher Name",
                a.IsActive))
            .ToList();

        return Task.FromResult<IReadOnlyList<VehicleAssignmentDto>>(list);
    }

    public Task AddAssignmentAsync(VehicleAssignment assignment, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAddAssignment && ExceptionToThrowOnAddAssignment != null)
        {
            throw ExceptionToThrowOnAddAssignment;
        }

        Assignments[assignment.Id] = assignment;
        return Task.CompletedTask;
    }

    public void UpdateAssignment(VehicleAssignment assignment)
    {
        Assignments[assignment.Id] = assignment;
    }

    public Task<IReadOnlyList<OdometerReadingDto>> GetOdometerReadingsByVehicleIdAsync(Guid vehicleId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var list = OdometerReadings
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.RecordedAt)
            .Take(limit)
            .Select(r => new OdometerReadingDto(
                r.Id,
                r.VehicleId,
                r.DriverId,
                "Driver Name",
                r.ReadingKm.Value,
                r.PhotoKey,
                r.RecordedAt,
                r.Source))
            .ToList();

        return Task.FromResult<IReadOnlyList<OdometerReadingDto>>(list);
    }

    public Task AddOdometerReadingAsync(OdometerReading reading, CancellationToken cancellationToken = default)
    {
        OdometerReadings.Add(reading);
        return Task.CompletedTask;
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

public sealed class FakeCurrentUser(
    Guid? userId = null,
    UserRole? role = UserRole.Dispatcher,
    string? ipAddress = "127.0.0.1",
    string? userAgent = "TestAgent") : ICurrentUser
{
    public Guid? UserId { get; set; } = userId ?? Guid.NewGuid();
    public UserRole? Role { get; set; } = role;
    public string? IpAddress { get; set; } = ipAddress;
    public string? UserAgent { get; set; } = userAgent;
    public bool IsAuthenticated => UserId.HasValue;
}

public sealed class FakeDateTimeProvider(DateTimeOffset? fixedUtcNow = null) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = fixedUtcNow ?? new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
    public DateTimeOffset NzNow => UtcNow.ToOffset(TimeSpan.FromHours(12));
    public DateOnly NzToday => DateOnly.FromDateTime(NzNow.DateTime);
}
