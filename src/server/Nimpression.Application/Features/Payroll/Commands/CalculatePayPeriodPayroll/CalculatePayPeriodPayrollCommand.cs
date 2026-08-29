using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Services;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Payroll.Commands.CalculatePayPeriodPayroll;

public sealed record CalculatePayPeriodPayrollCommand(
    Guid PayPeriodId,
    Guid? DriverId = null,
    IReadOnlySet<DateOnly>? PublicHolidays = null,
    decimal? MinimumHourlyWage = null) : IRequest<Result<IReadOnlyList<PayslipDto>>>, ICommandMarker, IAuditableCommand
{
    public string AuditEntityType => "PayPeriod";
    public Guid? AuditEntityId => PayPeriodId;
    public string AuditAction => "CalculatePayroll";
}

public sealed class CalculatePayPeriodPayrollCommandValidator : AbstractValidator<CalculatePayPeriodPayrollCommand>
{
    public CalculatePayPeriodPayrollCommandValidator()
    {
        RuleFor(x => x.PayPeriodId)
            .NotEmpty().WithMessage("Pay period ID is required.");
    }
}

public sealed class CalculatePayPeriodPayrollCommandHandler(
    Abstractions.IPayrollRepository payrollRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IAuditSink auditSink,
    IDateTimeProvider? dateTimeProvider = null) : IRequestHandler<CalculatePayPeriodPayrollCommand, Result<IReadOnlyList<PayslipDto>>>
{
    public async Task<Result<IReadOnlyList<PayslipDto>>> Handle(
        CalculatePayPeriodPayrollCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Dispatcher)
        {
            return Error.Forbidden("forbidden", "Only administrators or dispatchers can calculate payroll.");
        }

        var payPeriod = await payrollRepository.GetPayPeriodByIdAsync(request.PayPeriodId, cancellationToken);
        if (payPeriod is null)
        {
            return Error.NotFound("pay_period_not_found", $"Pay period with ID '{request.PayPeriodId}' was not found.");
        }

        // F7.8: 定版后不可改，只能作废重开
        if (payPeriod.Status == PayPeriodStatus.Finalised || payPeriod.Status == PayPeriodStatus.Paid)
        {
            return Error.Unprocessable(
                "period_finalised",
                $"Cannot calculate/modify payroll for a period in '{payPeriod.Status}' status. It must be voided/reopened first.");
        }

        List<Driver> drivers;
        if (request.DriverId.HasValue)
        {
            var driver = await payrollRepository.GetDriverByIdAsync(request.DriverId.Value, cancellationToken);
            if (driver is null)
            {
                return Error.NotFound("driver_not_found", $"Driver with ID '{request.DriverId.Value}' was not found.");
            }
            drivers = [driver];
        }
        else
        {
            var activeDrivers = await payrollRepository.GetActiveDriversAsync(cancellationToken);
            drivers = activeDrivers.ToList();
        }

        var calcTime = dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        var minWage = request.MinimumHourlyWage.HasValue
            ? new Money(request.MinimumHourlyWage.Value)
            : PayrollCalculatorV2.DefaultMinimumHourlyWage;

        var resultDtos = new List<PayslipDto>();

        foreach (var driver in drivers)
        {
            var shifts = await payrollRepository.GetCompletedShiftsForDriverAndPeriodAsync(
                driver.Id,
                payPeriod.StartsOn,
                payPeriod.EndsOn,
                cancellationToken);

            var tasks = await payrollRepository.GetCompletedJobTasksForDriverAndPeriodAsync(
                driver.Id,
                payPeriod.StartsOn,
                payPeriod.EndsOn,
                cancellationToken);

            var fines = await payrollRepository.GetFinesForDriverAndPeriodAsync(
                driver.Id,
                payPeriod.StartsOn,
                payPeriod.EndsOn,
                cancellationToken);

            // F7.8: 试算可重复（覆盖已有未定版工资单）
            var existingPayslip = await payrollRepository.GetPayslipByPeriodAndDriverAsync(
                payPeriod.Id,
                driver.Id,
                cancellationToken);

            if (existingPayslip is not null)
            {
                if (existingPayslip.FinalisedAt.HasValue)
                {
                    return Error.Unprocessable(
                        "payslip_finalised",
                        $"Payslip for driver '{driver.EmployeeNo}' in period '{payPeriod.Id}' is already finalised and cannot be recalculated.");
                }

                payrollRepository.RemovePayslip(existingPayslip);
            }

            var payslip = PayrollCalculatorV2.Calculate(
                driver: driver,
                payPeriod: payPeriod,
                shifts: shifts,
                tasks: tasks,
                publicHolidays: request.PublicHolidays,
                minimumHourlyWage: minWage,
                calculatedAt: calcTime);

            await payrollRepository.AddPayslipAsync(payslip, cancellationToken);

            var shiftDtos = shifts.Select(PayslipShiftDetailDto.FromEntity).ToList();
            var taskDtos = tasks.Select(PayslipTripDetailDto.FromEntity).ToList();
            var fineDtos = fines.Select(PayslipFineDto.FromEntity).ToList();

            resultDtos.Add(PayslipDto.FromEntity(
                payslip: payslip,
                startsOn: payPeriod.StartsOn,
                endsOn: payPeriod.EndsOn,
                driverName: null,
                employeeNo: driver.EmployeeNo,
                shiftDetails: shiftDtos,
                tripDetails: taskDtos,
                fines: fineDtos));
        }

        if (payPeriod.Status == PayPeriodStatus.Open)
        {
            payPeriod.SetStatus(PayPeriodStatus.Calculating);
            payrollRepository.UpdatePayPeriod(payPeriod);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await auditSink.RecordAsync(
            "PayPeriod",
            payPeriod.Id,
            "CalculatePayroll",
            null,
            $"{{\"payPeriodId\":\"{payPeriod.Id}\",\"driverCount\":{drivers.Count},\"calculatedAt\":\"{calcTime:O}\"}}",
            cancellationToken);

        return resultDtos;
    }
}
