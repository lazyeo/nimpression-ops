using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.Queries.GetPayPeriodPayslips;

public sealed record GetPayPeriodPayslipsQuery(Guid PayPeriodId) : IRequest<Result<IReadOnlyList<PayslipDto>>>;

public sealed class GetPayPeriodPayslipsQueryHandler(
    Abstractions.IPayrollRepository payrollRepository,
    ICurrentUser currentUser) : IRequestHandler<GetPayPeriodPayslipsQuery, Result<IReadOnlyList<PayslipDto>>>
{
    public async Task<Result<IReadOnlyList<PayslipDto>>> Handle(GetPayPeriodPayslipsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin)
        {
            return Error.Forbidden("forbidden", "Only administrators can view period payslips.");
        }

        var payPeriod = await payrollRepository.GetPayPeriodByIdAsync(request.PayPeriodId, cancellationToken);
        if (payPeriod is null)
        {
            return Error.NotFound("pay_period_not_found", $"Pay period with ID '{request.PayPeriodId}' was not found.");
        }

        var payslips = await payrollRepository.GetPayslipsByPeriodIdAsync(request.PayPeriodId, cancellationToken);
        var driverIds = payslips.Select(p => p.DriverId).Distinct().ToList();
        var driverNames = await payrollRepository.GetDriverDisplayNamesAsync(driverIds, cancellationToken);
        var result = new List<PayslipDto>();

        foreach (var payslip in payslips)
        {
            var driver = await payrollRepository.GetDriverByIdAsync(payslip.DriverId, cancellationToken);
            driverNames.TryGetValue(payslip.DriverId, out var driverName);
            result.Add(PayslipDto.FromEntity(
                payslip: payslip,
                startsOn: payPeriod.StartsOn,
                endsOn: payPeriod.EndsOn,
                driverName: driverName,
                employeeNo: driver?.EmployeeNo,
                paidAt: payPeriod.PaidAt));
        }

        return result;
    }
}
