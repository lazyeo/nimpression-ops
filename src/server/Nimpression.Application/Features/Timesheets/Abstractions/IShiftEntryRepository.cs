using Nimpression.Application.Features.Timesheets.DTOs;
using Nimpression.Domain.Entities.Timesheet;

namespace Nimpression.Application.Features.Timesheets.Abstractions;

/// <summary>
/// 班次打卡与工时仓储契约。
/// </summary>
public interface IShiftEntryRepository
{
    Task<ShiftEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ShiftEntry?> GetActiveShiftByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveShiftAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<Guid?> GetDriverIdByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task AddAsync(ShiftEntry shift, CancellationToken cancellationToken = default);

    void Update(ShiftEntry shift);

    Task<PagedResult<ShiftEntryDto>> GetShiftsPagedAsync(TimesheetFilter filter, CancellationToken cancellationToken = default);

    Task<TimesheetSummaryDto> GetSummaryAsync(TimesheetSummaryFilter filter, CancellationToken cancellationToken = default);
}
