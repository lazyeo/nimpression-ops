using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Fines.Abstractions;
using Nimpression.Application.Features.Fines.DTOs;
using Nimpression.Domain.Entities.Compliance;

namespace Nimpression.Infrastructure.Persistence.Repositories;

/// <summary>
/// 交通罚单仓储实现（F8 罚单）。
/// </summary>
public sealed class FineRepository(AppDbContext dbContext) : IFineRepository
{
    public async Task<Fine?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Fines
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Fines
            .AnyAsync(f => f.Id == id, cancellationToken);
    }

    public async Task AddAsync(Fine fine, CancellationToken cancellationToken = default)
    {
        await dbContext.Fines.AddAsync(fine, cancellationToken);
    }

    public async Task<PagedResult<FineDto>> GetFinesListAsync(
        FineFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Fines.AsNoTracking().AsQueryable();

        if (filter.DriverId.HasValue)
        {
            query = query.Where(f => f.DriverId == filter.DriverId.Value);
        }

        if (filter.VehicleId.HasValue)
        {
            query = query.Where(f => f.VehicleId == filter.VehicleId.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(f => f.Status == filter.Status.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(f => f.IssuedOn >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(f => f.IssuedOn <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            query = query.Where(f =>
                EF.Functions.ILike(f.Authority, $"%{search}%") ||
                EF.Functions.ILike(f.Reference, $"%{search}%") ||
                EF.Functions.ILike(f.Reason, $"%{search}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;

        var items = await (
            from fine in query.OrderByDescending(f => f.IssuedOn).ThenByDescending(f => f.Id).Skip((page - 1) * pageSize).Take(pageSize)
            join driver in dbContext.Drivers.AsNoTracking() on fine.DriverId equals driver.Id
            join user in dbContext.Users.AsNoTracking() on driver.UserId equals user.Id
            join vehicle in dbContext.Vehicles.AsNoTracking() on fine.VehicleId equals vehicle.Id
            select new FineDto(
                fine.Id,
                fine.DriverId,
                user.DisplayName,
                driver.EmployeeNo,
                fine.VehicleId,
                vehicle.Rego.Value,
                fine.IssuedOn,
                fine.Authority,
                fine.Reference,
                fine.Amount.Amount,
                fine.Amount.Currency,
                fine.Reason,
                fine.Status,
                fine.TicketPhotoKey,
                fine.ReviewedAt,
                fine.ReviewNote)
        ).ToListAsync(cancellationToken);

        return new PagedResult<FineDto>(items, totalCount, page, pageSize);
    }

    public async Task<FineDetailDto?> GetFineDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await (
            from fine in dbContext.Fines.AsNoTracking().Where(f => f.Id == id)
            join driver in dbContext.Drivers.AsNoTracking() on fine.DriverId equals driver.Id
            join user in dbContext.Users.AsNoTracking() on driver.UserId equals user.Id
            join vehicle in dbContext.Vehicles.AsNoTracking() on fine.VehicleId equals vehicle.Id
            from reviewer in dbContext.Users.AsNoTracking().Where(u => u.Id == fine.ReviewedByUserId).DefaultIfEmpty()
            select new FineDetailDto(
                fine.Id,
                fine.DriverId,
                user.DisplayName,
                driver.EmployeeNo,
                fine.VehicleId,
                vehicle.Rego.Value,
                fine.IssuedOn,
                fine.Authority,
                fine.Reference,
                fine.Amount.Amount,
                fine.Amount.Currency,
                fine.Reason,
                fine.Status,
                fine.TicketPhotoKey,
                null,
                fine.ReviewedByUserId,
                reviewer != null ? reviewer.DisplayName : null,
                fine.ReviewedAt,
                fine.ReviewNote)
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
