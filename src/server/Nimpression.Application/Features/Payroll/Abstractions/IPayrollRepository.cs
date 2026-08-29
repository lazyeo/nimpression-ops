using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Entities.Timesheet;

namespace Nimpression.Application.Features.Payroll.Abstractions;

/// <summary>
/// 薪资聚合与支撑数据仓储契约。
/// </summary>
public interface IPayrollRepository
{
    Task<PayPeriod?> GetPayPeriodByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<PayPeriodDto>> GetPayPeriodsListAsync(PayPeriodFilter filter, CancellationToken cancellationToken = default);

    Task<bool> HasOverlappingPayPeriodAsync(DateOnly startsOn, DateOnly endsOn, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task AddPayPeriodAsync(PayPeriod payPeriod, CancellationToken cancellationToken = default);

    void UpdatePayPeriod(PayPeriod payPeriod);

    Task<Payslip?> GetPayslipByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Payslip?> GetPayslipByPeriodAndDriverAsync(Guid payPeriodId, Guid driverId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payslip>> GetPayslipsByPeriodIdAsync(Guid payPeriodId, CancellationToken cancellationToken = default);

    Task<PagedResult<PayslipDto>> GetPayslipsForDriverPagedAsync(DriverPayslipsFilter filter, CancellationToken cancellationToken = default);

    Task AddPayslipAsync(Payslip payslip, CancellationToken cancellationToken = default);

    void RemovePayslip(Payslip payslip);

    void RemovePayslips(IEnumerable<Payslip> payslips);

    Task<Driver?> GetDriverByIdAsync(Guid driverId, CancellationToken cancellationToken = default);

    Task<Driver?> GetDriverByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Driver>> GetActiveDriversAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftEntry>> GetCompletedShiftsForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobTask>> GetCompletedJobTasksForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Fine>> GetFinesForDriverAndPeriodAsync(
        Guid driverId,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default);
}
