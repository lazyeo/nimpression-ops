using Nimpression.Application.Features.Drivers.DTOs;
using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Drivers.Abstractions;

/// <summary>
/// 司机仓储契约。封装司机聚合与关联实体的查询与持久化。
/// </summary>
public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Driver?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Driver?> GetByEmployeeNoAsync(string employeeNo, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmployeeNoAsync(string employeeNo, CancellationToken cancellationToken = default);

    Task<User?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);

    Task AddDriverAsync(
        Driver driver,
        User user,
        IEnumerable<AreaAssignment>? initialAssignments = null,
        CancellationToken cancellationToken = default);

    void UpdateDriver(Driver driver);

    void UpdateUser(User user);

    Task<PagedResult<DriverSummaryDto>> GetDriversPagedAsync(
        DriverFilter filter,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default);

    Task<DriverDetailDto?> GetDriverDetailByIdAsync(
        Guid id,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default);

    Task<List<DriverLicenceAlertDto>> GetExpiringLicencesAsync(
        DateOnly referenceDate,
        int daysThreshold = 30,
        CancellationToken cancellationToken = default);

    Task<List<AreaAssignmentDto>> GetDriverAreaAssignmentsAsync(
        Guid driverId,
        DateOnly referenceDate,
        CancellationToken cancellationToken = default);
}
