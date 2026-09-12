using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Application.Features.Payroll.TaxProfiles;
using Nimpression.Domain.Entities.Payroll;

namespace Nimpression.Infrastructure.Persistence.Repositories;

public sealed class TaxProfileRepository(AppDbContext db) : ITaxProfileRepository
{
    public async Task LockDriverAsync(Guid driverId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Tax profile mutations require a transaction.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Drivers\" WHERE \"Id\" = {driverId} FOR UPDATE", ct);
    }
    public Task<Guid?> PayslipDriverIdAsync(Guid payslipId, CancellationToken ct) => db.Payslips
        .Where(p => p.Id == payslipId).Select(p => (Guid?)p.DriverId).SingleOrDefaultAsync(ct);
    public async Task<DriverTaxProfile?> GetAsync(Guid id, CancellationToken ct)
    {
        var tracked = db.DriverTaxProfiles.Local.SingleOrDefault(p => p.Id == id);
        if (tracked is not null) await db.Entry(tracked).ReloadAsync(ct);
        return await db.DriverTaxProfiles.SingleOrDefaultAsync(p => p.Id == id, ct);
    }
    public Task<DriverTaxProfile?> LatestApprovedAsync(Guid driverId, DateOnly payDate, CancellationToken ct) => db.DriverTaxProfiles.AsNoTracking()
        .Where(p => p.DriverId == driverId && p.Status == DriverTaxProfileStatus.Approved && p.EffectiveFrom <= payDate)
        .OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(ct);
    public Task<bool> HasPendingAsync(Guid driverId, CancellationToken ct) => db.DriverTaxProfiles
        .AnyAsync(p => p.DriverId == driverId && p.Status == DriverTaxProfileStatus.Pending, ct);
    public Task<bool> HasApprovedDateAsync(Guid driverId, DateOnly effectiveFrom, CancellationToken ct) => db.DriverTaxProfiles
        .AnyAsync(p => p.DriverId == driverId && p.Status == DriverTaxProfileStatus.Approved && p.EffectiveFrom == effectiveFrom, ct);
    public void Add(DriverTaxProfile profile) => db.DriverTaxProfiles.Add(profile);
    private IQueryable<DriverTaxProfileDto> Project(IQueryable<DriverTaxProfile> profiles) =>
        from p in profiles
        join d in db.Drivers on p.DriverId equals d.Id
        join u in db.Users on d.UserId equals u.Id
        orderby p.SubmittedAt descending, p.Id descending
        select new DriverTaxProfileDto(p.Id, p.DriverId, u.DisplayName, d.EmployeeNo, p.EffectiveFrom, p.Status,
            p.Declaration, p.SubmittedAt, p.ReviewedAt, p.ApprovedEmployee, p.ApprovedContractor);
    public Task<DriverTaxProfileDto> GetDtoAsync(Guid id, CancellationToken ct) =>
        Project(db.DriverTaxProfiles.AsNoTracking().Where(p => p.Id == id)).SingleAsync(ct);
    public async Task<PagedResult<DriverTaxProfileDto>> ListAsync(Guid? driverId, DriverTaxProfileStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.DriverTaxProfiles.AsNoTracking();
        if (driverId.HasValue) query = query.Where(p => p.DriverId == driverId.Value);
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        var count = await query.CountAsync(ct);
        var pageQuery = query.OrderByDescending(p => p.SubmittedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize);
        var items = await Project(pageQuery).ToListAsync(ct);
        return new(items, count, page, pageSize);
    }
}
