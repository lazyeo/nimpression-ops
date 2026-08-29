using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Areas.Abstractions;
using Nimpression.Application.Features.Areas.DTOs;
using Nimpression.Domain.Entities.Area;

namespace Nimpression.Infrastructure.Persistence.Repositories;

/// <summary>
/// 运营区域仓储实现。
/// </summary>
public sealed class AreaRepository(AppDbContext dbContext) : IAreaRepository
{
    public async Task<Area?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Areas.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Area?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await dbContext.Areas.FirstOrDefaultAsync(a => a.Code == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await dbContext.Areas.AnyAsync(
            a => a.Code == normalized && (!excludeId.HasValue || a.Id != excludeId.Value),
            cancellationToken);
    }

    public async Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);
    }

    public async Task<bool> HasActiveAssignmentsAsync(Guid areaId, DateOnly referenceDate, CancellationToken cancellationToken = default)
    {
        return await dbContext.AreaAssignments.AnyAsync(
            aa => aa.AreaId == areaId && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate),
            cancellationToken);
    }

    public async Task AddAreaAsync(Area area, CancellationToken cancellationToken = default)
    {
        await dbContext.Areas.AddAsync(area, cancellationToken);
    }

    public void UpdateArea(Area area)
    {
        dbContext.Areas.Update(area);
    }

    public void DeleteArea(Area area)
    {
        dbContext.Areas.Remove(area);
    }

    public async Task<PagedResult<AreaDto>> GetAreasPagedAsync(AreaFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Areas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var search = filter.SearchTerm.Trim();
            query = query.Where(a => EF.Functions.ILike(a.Name, $"%{search}%") || EF.Functions.ILike(a.Code, $"%{search}%"));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(a => a.IsActive == filter.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderBy(a => a.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AreaDto(
                a.Id,
                a.Name,
                a.Code,
                a.Description,
                a.GeoJson,
                a.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<AreaDto>(items, totalCount, page, pageSize);
    }

    public async Task<AreaDetailDto?> GetAreaDetailByIdAsync(Guid id, DateOnly referenceDate, CancellationToken cancellationToken = default)
    {
        return await dbContext.Areas.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AreaDetailDto(
                a.Id,
                a.Name,
                a.Code,
                a.Description,
                a.GeoJson,
                a.IsActive,
                dbContext.AreaAssignments.Count(aa => aa.AreaId == a.Id && aa.EffectiveFrom <= referenceDate && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate))))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<AreaAssignment>> GetAssignmentsForDriverAndAreaAsync(Guid driverId, Guid areaId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AreaAssignments
            .Where(aa => aa.DriverId == driverId && aa.AreaId == areaId)
            .ToListAsync(cancellationToken);
    }

    public async Task<AreaAssignment?> GetAssignmentByIdAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AreaAssignments.FirstOrDefaultAsync(aa => aa.Id == assignmentId, cancellationToken);
    }

    public async Task AddAssignmentAsync(AreaAssignment assignment, CancellationToken cancellationToken = default)
    {
        await dbContext.AreaAssignments.AddAsync(assignment, cancellationToken);
    }

    public void DeleteAssignment(AreaAssignment assignment)
    {
        dbContext.AreaAssignments.Remove(assignment);
    }

    public async Task<List<AreaAssignmentDto>> GetAreaAssignmentsAsync(Guid? areaId, Guid? driverId, DateOnly referenceDate, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AreaAssignments.AsNoTracking().AsQueryable();

        if (areaId.HasValue)
        {
            query = query.Where(aa => aa.AreaId == areaId.Value);
        }

        if (driverId.HasValue)
        {
            query = query.Where(aa => aa.DriverId == driverId.Value);
        }

        return await query
            .Join(dbContext.Areas.AsNoTracking(), aa => aa.AreaId, a => a.Id, (aa, a) => new { aa, a })
            .OrderByDescending(x => x.aa.EffectiveFrom)
            .Select(x => new AreaAssignmentDto(
                x.aa.Id,
                x.aa.AreaId,
                x.a.Name,
                x.a.Code,
                x.aa.DriverId,
                x.aa.EffectiveFrom,
                x.aa.EffectiveTo,
                x.aa.EffectiveFrom <= referenceDate && (x.aa.EffectiveTo == null || x.aa.EffectiveTo >= referenceDate)))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsDriverAssignedToAreaOnDateAsync(Guid driverId, Guid areaId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return await dbContext.AreaAssignments.AsNoTracking().AnyAsync(
            aa => aa.DriverId == driverId && aa.AreaId == areaId && aa.EffectiveFrom <= date && (aa.EffectiveTo == null || aa.EffectiveTo >= date),
            cancellationToken);
    }
}
