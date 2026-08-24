using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Vehicles.Abstractions;
using Nimpression.Application.Features.Vehicles.DTOs;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Integration.Tests.Vehicles.Persistence;

/// <summary>
/// 基于 EF Core AppDbContext 的车辆仓储实现。
/// </summary>
public sealed class EfVehicleRepository(AppDbContext context) : IVehicleRepository
{
    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Vehicle?> GetByRegoAsync(Rego rego, CancellationToken cancellationToken = default)
    {
        return await context.Vehicles.FirstOrDefaultAsync(v => v.Rego == rego, cancellationToken);
    }

    public async Task<bool> ExistsByRegoAsync(Rego rego, CancellationToken cancellationToken = default)
    {
        return await context.Vehicles.AnyAsync(v => v.Rego == rego, cancellationToken);
    }

    public async Task<PagedResult<VehicleSummaryDto>> GetVehiclesPagedAsync(VehicleFilter filter, CancellationToken cancellationToken = default)
    {
        var query = context.Vehicles.AsNoTracking();

        if (filter.Status.HasValue)
        {
            query = query.Where(v => v.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(v =>
                EF.Functions.ILike((string)v.Rego, pattern) ||
                EF.Functions.ILike(v.Make, pattern) ||
                EF.Functions.ILike(v.Model, pattern));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await (from v in query
                           join a in context.VehicleAssignments.AsNoTracking().Where(x => x.ReleasedAt == null)
                               on v.Id equals a.VehicleId into aj
                           from a in aj.DefaultIfEmpty()
                           join d in context.Drivers.AsNoTracking()
                               on a.DriverId equals d.Id into dj
                           from d in dj.DefaultIfEmpty()
                           join u in context.Users.AsNoTracking()
                               on d.UserId equals u.Id into uj
                           from u in uj.DefaultIfEmpty()
                           orderby v.Rego
                           select new VehicleSummaryDto(
                               v.Id,
                               v.Rego.Value,
                               v.Make,
                               v.Model,
                               v.Year,
                               v.OdometerKm.Value,
                               v.ServiceIntervalKm.Value,
                               v.LastServiceOdometerKm.Value,
                               v.OdometerKm.Value - v.LastServiceOdometerKm.Value,
                               v.OdometerKm.Value - v.LastServiceOdometerKm.Value >= v.ServiceIntervalKm.Value,
                               v.WofExpiry,
                               v.CofExpiry,
                               v.InsuranceExpiry,
                               v.Status,
                               a != null ? a.DriverId : null,
                               u != null ? u.DisplayName : null))
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<VehicleSummaryDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<VehicleDetailDto?> GetVehicleDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (vehicle is null)
        {
            return null;
        }

        var activeAssignment = await (from a in context.VehicleAssignments.AsNoTracking()
                                      where a.VehicleId == id && a.ReleasedAt == null
                                      join d in context.Drivers.AsNoTracking() on a.DriverId equals d.Id into dj
                                      from d in dj.DefaultIfEmpty()
                                      join u in context.Users.AsNoTracking() on d.UserId equals u.Id into uj
                                      from u in uj.DefaultIfEmpty()
                                      join assigner in context.Users.AsNoTracking() on a.AssignedByUserId equals assigner.Id into assj
                                      from assigner in assj.DefaultIfEmpty()
                                      select new VehicleAssignmentDto(
                                          a.Id,
                                          a.VehicleId,
                                          vehicle.Rego.Value,
                                          a.DriverId,
                                          u != null ? u.DisplayName : null,
                                          d != null ? d.EmployeeNo : null,
                                          a.AssignedAt,
                                          a.ReleasedAt,
                                          a.AssignedByUserId,
                                          assigner != null ? assigner.DisplayName : null,
                                          a.ReleasedAt == null))
            .FirstOrDefaultAsync(cancellationToken);

        var latestReading = await (from r in context.OdometerReadings.AsNoTracking()
                                   where r.VehicleId == id
                                   orderby r.RecordedAt descending
                                   join d in context.Drivers.AsNoTracking() on r.DriverId equals d.Id into dj
                                   from d in dj.DefaultIfEmpty()
                                   join u in context.Users.AsNoTracking() on d.UserId equals u.Id into uj
                                   from u in uj.DefaultIfEmpty()
                                   select new OdometerReadingDto(
                                       r.Id,
                                       r.VehicleId,
                                       r.DriverId,
                                       u != null ? u.DisplayName : null,
                                       r.ReadingKm.Value,
                                       r.PhotoKey,
                                       r.RecordedAt,
                                       r.Source))
            .FirstOrDefaultAsync(cancellationToken);

        return new VehicleDetailDto(
            vehicle.Id,
            vehicle.Rego.Value,
            vehicle.Make,
            vehicle.Model,
            vehicle.Year,
            vehicle.VinEnc,
            vehicle.OdometerKm.Value,
            vehicle.ServiceIntervalKm.Value,
            vehicle.LastServiceOdometerKm.Value,
            vehicle.DistanceSinceLastService.Value,
            vehicle.IsServiceDue,
            vehicle.WofExpiry,
            vehicle.CofExpiry,
            vehicle.InsuranceExpiry,
            vehicle.Status,
            activeAssignment,
            latestReading);
    }

    public async Task AddVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await context.Vehicles.AddAsync(vehicle, cancellationToken);
    }

    public void UpdateVehicle(Vehicle vehicle)
    {
        context.Vehicles.Update(vehicle);
    }

    public async Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await context.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);
    }

    public async Task<VehicleAssignment?> GetAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.VehicleAssignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<VehicleAssignment?> GetActiveAssignmentByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await context.VehicleAssignments.FirstOrDefaultAsync(a => a.VehicleId == vehicleId && a.ReleasedAt == null, cancellationToken);
    }

    public async Task<IReadOnlyList<VehicleAssignmentDto>> GetAssignmentsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var items = await (from a in context.VehicleAssignments.AsNoTracking()
                           where a.VehicleId == vehicleId
                           join v in context.Vehicles.AsNoTracking() on a.VehicleId equals v.Id into vj
                           from v in vj.DefaultIfEmpty()
                           join d in context.Drivers.AsNoTracking() on a.DriverId equals d.Id into dj
                           from d in dj.DefaultIfEmpty()
                           join u in context.Users.AsNoTracking() on d.UserId equals u.Id into uj
                           from u in uj.DefaultIfEmpty()
                           join assigner in context.Users.AsNoTracking() on a.AssignedByUserId equals assigner.Id into assj
                           from assigner in assj.DefaultIfEmpty()
                           orderby a.AssignedAt descending
                           select new VehicleAssignmentDto(
                               a.Id,
                               a.VehicleId,
                               v != null ? v.Rego.Value : null,
                               a.DriverId,
                               u != null ? u.DisplayName : null,
                               d != null ? d.EmployeeNo : null,
                               a.AssignedAt,
                               a.ReleasedAt,
                               a.AssignedByUserId,
                               assigner != null ? assigner.DisplayName : null,
                               a.ReleasedAt == null))
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task AddAssignmentAsync(VehicleAssignment assignment, CancellationToken cancellationToken = default)
    {
        await context.VehicleAssignments.AddAsync(assignment, cancellationToken);
    }

    public void UpdateAssignment(VehicleAssignment assignment)
    {
        context.VehicleAssignments.Update(assignment);
    }

    public async Task<IReadOnlyList<OdometerReadingDto>> GetOdometerReadingsByVehicleIdAsync(Guid vehicleId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var items = await (from r in context.OdometerReadings.AsNoTracking()
                           where r.VehicleId == vehicleId
                           orderby r.RecordedAt descending
                           join d in context.Drivers.AsNoTracking() on r.DriverId equals d.Id into dj
                           from d in dj.DefaultIfEmpty()
                           join u in context.Users.AsNoTracking() on d.UserId equals u.Id into uj
                           from u in uj.DefaultIfEmpty()
                           select new OdometerReadingDto(
                               r.Id,
                               r.VehicleId,
                               r.DriverId,
                               u != null ? u.DisplayName : null,
                               r.ReadingKm.Value,
                               r.PhotoKey,
                               r.RecordedAt,
                               r.Source))
            .Take(limit)
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task AddOdometerReadingAsync(OdometerReading reading, CancellationToken cancellationToken = default)
    {
        await context.OdometerReadings.AddAsync(reading, cancellationToken);
    }
}
