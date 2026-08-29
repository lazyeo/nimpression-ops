using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.Queries.GetPayslipById;

public sealed record GetPayslipByIdQuery(Guid PayslipId) : IRequest<Result<PayslipDto>>;

public sealed class GetPayslipByIdQueryHandler(
    Abstractions.IPayrollRepository payrollRepository,
    ICurrentUser currentUser) : IRequestHandler<GetPayslipByIdQuery, Result<PayslipDto>>
{
    public async Task<Result<PayslipDto>> Handle(GetPayslipByIdQuery request, CancellationToken cancellationToken)
    {
        var payslip = await payrollRepository.GetPayslipByIdAsync(request.PayslipId, cancellationToken);
        if (payslip is null)
        {
            return Error.NotFound("payslip_not_found", $"Payslip with ID '{request.PayslipId}' was not found.");
        }

        var driver = await payrollRepository.GetDriverByIdAsync(payslip.DriverId, cancellationToken);
        var payPeriod = await payrollRepository.GetPayPeriodByIdAsync(payslip.PayPeriodId, cancellationToken);

        // F7.10: 司机只能看自己已定版的工资单；查他人 403（不是 404）
        if (currentUser.Role == UserRole.Driver)
        {
            var currentDriver = currentUser.UserId.HasValue
                ? await payrollRepository.GetDriverByUserIdAsync(currentUser.UserId.Value, cancellationToken)
                : null;

            if (currentDriver is null || currentDriver.Id != payslip.DriverId)
            {
                return Error.Forbidden(
                    "forbidden",
                    "Drivers are forbidden from viewing payslips belonging to other drivers.");
            }

            if (!payslip.FinalisedAt.HasValue)
            {
                return Error.Forbidden(
                    "payslip_not_finalised",
                    "Drivers can only view finalised payslips.");
            }
        }
        else if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Dispatcher)
        {
            return Error.Forbidden("forbidden", "Unauthorized to view payslips.");
        }

        var startsOn = payPeriod?.StartsOn ?? DateOnly.MinValue;
        var endsOn = payPeriod?.EndsOn ?? DateOnly.MaxValue;

        // F7.11: 工时明细可追溯到 ShiftEntry，趟次明细可追溯到 JobTask
        var shifts = await payrollRepository.GetCompletedShiftsForDriverAndPeriodAsync(
            payslip.DriverId,
            startsOn,
            endsOn,
            cancellationToken);

        var tasks = await payrollRepository.GetCompletedJobTasksForDriverAndPeriodAsync(
            payslip.DriverId,
            startsOn,
            endsOn,
            cancellationToken);

        // F7.12: 工资单金额与罚款无计算关联；UI/API 分区展示并附法规说明
        var fines = await payrollRepository.GetFinesForDriverAndPeriodAsync(
            payslip.DriverId,
            startsOn,
            endsOn,
            cancellationToken);

        var shiftDtos = shifts.Select(PayslipShiftDetailDto.FromEntity).ToList();
        var taskDtos = tasks.Select(PayslipTripDetailDto.FromEntity).ToList();
        var fineDtos = fines.Select(PayslipFineDto.FromEntity).ToList();

        return PayslipDto.FromEntity(
            payslip: payslip,
            startsOn: startsOn,
            endsOn: endsOn,
            driverName: null,
            employeeNo: driver?.EmployeeNo,
            shiftDetails: shiftDtos,
            tripDetails: taskDtos,
            fines: fineDtos);
    }
}
