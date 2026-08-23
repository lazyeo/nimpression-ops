using Microsoft.EntityFrameworkCore;
using Nimpression.Application.Features.Drivers.Abstractions;
using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure.Storage;

/// <summary>
/// 司机仓储实现。使用 EF Core 投影查询杜绝 N+1 问题。
/// </summary>
public sealed class DriverRepository(AppDbContext dbContext) : IDriverRepository
{
    public async Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
    }

    public async Task<Driver?> GetByEmployeeNoAsync(string employeeNo, CancellationToken cancellationToken = default)
    {
        var normalized = employeeNo.Trim().ToUpperInvariant();
        return await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.EmployeeNo == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByEmployeeNoAsync(string employeeNo, CancellationToken cancellationToken = default)
    {
        var normalized = employeeNo.Trim().ToUpperInvariant();
        return await dbContext.Drivers
            .AnyAsync(d => d.EmployeeNo == normalized, cancellationToken);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task AddDriverAsync(
        Driver driver,
        User user,
        IEnumerable<AreaAssignment>? initialAssignments = null,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.Drivers.AddAsync(driver, cancellationToken);

        if (initialAssignments != null)
        {
            await dbContext.AreaAssignments.AddRangeAsync(initialAssignments, cancellationToken);
        }
    }

    public void UpdateDriver(Driver driver)
    {
        dbContext.Drivers.Update(driver);
    }

    public void UpdateUser(User user)
    {
        dbContext.Users.Update(user);
    }

    public async Task<PagedResult<DriverSummaryDto>> GetDriversPagedAsync(
        DriverFilter filter,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default)
    {
        var query = from d in dbContext.Drivers.AsNoTracking()
                    join u in dbContext.Users.AsNoTracking() on d.UserId equals u.Id
                    select new { Driver = d, User = u };

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();
            query = query.Where(x => x.User.DisplayName.Contains(term) || x.Driver.EmployeeNo.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var name = filter.Name.Trim();
            query = query.Where(x => x.User.DisplayName.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(filter.EmployeeNo))
        {
            var empNo = filter.EmployeeNo.Trim().ToUpperInvariant();
            query = query.Where(x => x.Driver.EmployeeNo.Contains(empNo));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.Driver.Status == filter.Status.Value);
        }

        if (filter.AreaId.HasValue)
        {
            var areaId = filter.AreaId.Value;
            query = query.Where(x => dbContext.AreaAssignments.Any(aa =>
                aa.DriverId == x.Driver.Id &&
                aa.AreaId == areaId &&
                aa.EffectiveFrom <= referenceDate &&
                (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = filter.Page > 0 ? filter.Page : 1;
        var pageSize = filter.PageSize > 0 ? filter.PageSize : 20;

        var rawItems = await query
            .OrderBy(x => x.Driver.EmployeeNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Driver.Id,
                x.Driver.UserId,
                x.Driver.EmployeeNo,
                x.User.DisplayName,
                Email = x.User.Email.Value,
                x.Driver.LicenceClass,
                x.Driver.LicenceExpiry,
                x.Driver.Status,
                x.Driver.HiredOn,
                HourlyRate = x.Driver.HourlyRate.Amount,
                PerTripRate = x.Driver.PerTripRate.Amount,
                PerKmRate = x.Driver.PerKmRate.Amount,
                x.User.AvatarKey,
                ActiveAreaIds = dbContext.AreaAssignments
                    .Where(aa => aa.DriverId == x.Driver.Id && aa.EffectiveFrom <= referenceDate && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate))
                    .Select(aa => aa.AreaId)
                    .ToList(),
                ActiveAreaNames = (from aa in dbContext.AreaAssignments
                                   join a in dbContext.Areas on aa.AreaId equals a.Id
                                   where aa.DriverId == x.Driver.Id && aa.EffectiveFrom <= referenceDate && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate)
                                   select a.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        var dtos = rawItems.Select(x =>
        {
            var isExpired = referenceDate > x.LicenceExpiry;
            var daysRemaining = x.LicenceExpiry.DayNumber - referenceDate.DayNumber;
            var isExpiringSoon = daysRemaining >= 0 && daysRemaining <= 30;

            return new DriverSummaryDto(
                x.Id,
                x.UserId,
                x.EmployeeNo,
                x.DisplayName,
                x.Email,
                x.LicenceClass,
                x.LicenceExpiry,
                isExpiringSoon,
                isExpired,
                daysRemaining,
                x.Status,
                x.HiredOn,
                x.HourlyRate,
                x.PerTripRate,
                x.PerKmRate,
                x.ActiveAreaNames,
                x.ActiveAreaIds,
                null);
        }).ToList();

        return new PagedResult<DriverSummaryDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<DriverDetailDto?> GetDriverDetailByIdAsync(
        Guid id,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default)
    {
        var raw = await (from d in dbContext.Drivers.AsNoTracking()
                         join u in dbContext.Users.AsNoTracking() on d.UserId equals u.Id
                         where d.Id == id
                         select new
                         {
                             Driver = d,
                             User = u
                         }).FirstOrDefaultAsync(cancellationToken);

        if (raw is null)
        {
            return null;
        }

        var areaAssignments = await (from aa in dbContext.AreaAssignments.AsNoTracking()
                                     join a in dbContext.Areas.AsNoTracking() on aa.AreaId equals a.Id
                                     where aa.DriverId == id
                                     orderby aa.EffectiveFrom descending
                                     select new AreaAssignmentDto(
                                         aa.Id,
                                         a.Id,
                                         a.Name,
                                         a.Code,
                                         aa.EffectiveFrom,
                                         aa.EffectiveTo,
                                         aa.EffectiveFrom <= referenceDate && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate)))
                                    .ToListAsync(cancellationToken);

        var isExpired = referenceDate > raw.Driver.LicenceExpiry;
        var daysRemaining = raw.Driver.LicenceExpiry.DayNumber - referenceDate.DayNumber;
        var isExpiringSoon = daysRemaining >= 0 && daysRemaining <= 30;

        // 解密显示（如存在 ENC 前缀则保留或展示明文主体）
        var phone = StripEnc(raw.Driver.PhoneEnc);
        var addr = StripEnc(raw.Driver.AddressEnc);
        var emg = StripEnc(raw.Driver.EmergencyContactEnc);

        return new DriverDetailDto(
            raw.Driver.Id,
            raw.User.Id,
            raw.Driver.EmployeeNo,
            raw.User.DisplayName,
            raw.User.Email.Value,
            raw.Driver.LicenceClass,
            raw.Driver.LicenceExpiry,
            isExpiringSoon,
            isExpired,
            daysRemaining,
            raw.Driver.Status,
            raw.Driver.HiredOn,
            raw.Driver.HourlyRate.Amount,
            raw.Driver.HourlyRate.Currency,
            raw.Driver.PerTripRate.Amount,
            raw.Driver.PerTripRate.Currency,
            raw.Driver.PerKmRate.Amount,
            raw.Driver.PerKmRate.Currency,
            phone,
            addr,
            emg,
            raw.User.Locale,
            raw.User.AvatarKey,
            null,
            areaAssignments);
    }

    public async Task<List<DriverLicenceAlertDto>> GetExpiringLicencesAsync(
        DateOnly referenceDate,
        int daysThreshold = 30,
        CancellationToken cancellationToken = default)
    {
        var thresholdDate = referenceDate.AddDays(daysThreshold);

        var alerts = await (from d in dbContext.Drivers.AsNoTracking()
                            join u in dbContext.Users.AsNoTracking() on d.UserId equals u.Id
                            where d.Status == DriverStatus.Active && d.LicenceExpiry <= thresholdDate
                            orderby d.LicenceExpiry
                            select new
                            {
                                DriverId = d.Id,
                                UserId = u.Id,
                                d.EmployeeNo,
                                u.DisplayName,
                                d.LicenceClass,
                                d.LicenceExpiry,
                                d.Status
                            }).ToListAsync(cancellationToken);

        return alerts.Select(x =>
        {
            var isExpired = referenceDate > x.LicenceExpiry;
            var daysRemaining = x.LicenceExpiry.DayNumber - referenceDate.DayNumber;

            return new DriverLicenceAlertDto(
                x.DriverId,
                x.UserId,
                x.EmployeeNo,
                x.DisplayName,
                x.LicenceClass,
                x.LicenceExpiry,
                daysRemaining,
                isExpired,
                x.Status);
        }).ToList();
    }

    public async Task<List<AreaAssignmentDto>> GetDriverAreaAssignmentsAsync(
        Guid driverId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default)
    {
        return await (from aa in dbContext.AreaAssignments.AsNoTracking()
                      join a in dbContext.Areas.AsNoTracking() on aa.AreaId equals a.Id
                      where aa.DriverId == driverId
                      orderby aa.EffectiveFrom descending
                      select new AreaAssignmentDto(
                          aa.Id,
                          a.Id,
                          a.Name,
                          a.Code,
                          aa.EffectiveFrom,
                          aa.EffectiveTo,
                          aa.EffectiveFrom <= referenceDate && (aa.EffectiveTo == null || aa.EffectiveTo >= referenceDate)))
                     .ToListAsync(cancellationToken);
    }

    private static string StripEnc(string val)
    {
        if (string.IsNullOrEmpty(val)) return string.Empty;
        if (val.StartsWith("ENC(", StringComparison.Ordinal) && val.EndsWith(')'))
        {
            return val[4..^1];
        }
        return val;
    }
}
