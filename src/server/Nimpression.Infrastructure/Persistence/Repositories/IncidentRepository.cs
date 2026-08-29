using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Incidents.Abstractions;
using Nimpression.Application.Features.Incidents.DTOs;
using Nimpression.Domain.Entities.Compliance;

namespace Nimpression.Infrastructure.Persistence.Repositories;

/// <summary>
/// 事故报告仓储实现（F9 事故）。
/// </summary>
public sealed class IncidentRepository(AppDbContext dbContext) : IIncidentRepository
{
    public async Task<IncidentReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.IncidentReports
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.IncidentReports
            .AnyAsync(i => i.Id == id, cancellationToken);
    }

    public async Task AddAsync(IncidentReport report, CancellationToken cancellationToken = default)
    {
        await dbContext.IncidentReports.AddAsync(report, cancellationToken);
    }

    public async Task<PagedResult<IncidentReportDto>> GetIncidentsListAsync(
        IncidentFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.IncidentReports.AsNoTracking().AsQueryable();

        if (filter.DriverId.HasValue)
        {
            query = query.Where(i => i.DriverId == filter.DriverId.Value);
        }

        if (filter.VehicleId.HasValue)
        {
            query = query.Where(i => i.VehicleId == filter.VehicleId.Value);
        }

        if (filter.Severity.HasValue)
        {
            query = query.Where(i => i.Severity == filter.Severity.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(i => i.OccurredAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(i => i.OccurredAt <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            query = query.Where(i =>
                EF.Functions.ILike(i.Location, $"%{search}%") ||
                EF.Functions.ILike(i.Description, $"%{search}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;

        var items = await (
            from inc in query.OrderByDescending(i => i.OccurredAt).ThenByDescending(i => i.Id).Skip((page - 1) * pageSize).Take(pageSize)
            join driver in dbContext.Drivers.AsNoTracking() on inc.DriverId equals driver.Id
            join user in dbContext.Users.AsNoTracking() on driver.UserId equals user.Id
            join vehicle in dbContext.Vehicles.AsNoTracking() on inc.VehicleId equals vehicle.Id
            select new IncidentReportDto(
                inc.Id,
                inc.DriverId,
                user.DisplayName,
                driver.EmployeeNo,
                inc.VehicleId,
                vehicle.Rego.Value,
                inc.OccurredAt,
                inc.Location,
                inc.Severity,
                inc.Description,
                inc.ThirdPartyInfoEnc,
                inc.Status,
                inc.InsurerNotifiedAt,
                inc.PhotoKeys,
                inc.InsurerNotifiedAt.HasValue)
        ).ToListAsync(cancellationToken);

        return new PagedResult<IncidentReportDto>(items, totalCount, page, pageSize);
    }

    public async Task<IncidentReportDetailDto?> GetIncidentDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await (
            from inc in dbContext.IncidentReports.AsNoTracking().Where(i => i.Id == id)
            join driver in dbContext.Drivers.AsNoTracking() on inc.DriverId equals driver.Id
            join user in dbContext.Users.AsNoTracking() on driver.UserId equals user.Id
            join vehicle in dbContext.Vehicles.AsNoTracking() on inc.VehicleId equals vehicle.Id
            select new IncidentReportDetailDto(
                inc.Id,
                inc.DriverId,
                user.DisplayName,
                driver.EmployeeNo,
                inc.VehicleId,
                vehicle.Rego.Value,
                inc.OccurredAt,
                inc.Location,
                inc.Severity,
                inc.Description,
                inc.ThirdPartyInfoEnc,
                inc.Status,
                inc.InsurerNotifiedAt,
                inc.PhotoKeys,
                new List<string>(),
                inc.InsurerNotifiedAt.HasValue)
        ).FirstOrDefaultAsync(cancellationToken);

        return result;
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

    public async Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Vehicles
            .AnyAsync(v => v.Id == vehicleId, cancellationToken);
    }
}
