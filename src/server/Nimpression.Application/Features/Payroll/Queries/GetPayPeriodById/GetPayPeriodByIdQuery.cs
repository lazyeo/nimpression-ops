using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.Queries.GetPayPeriodById;

public sealed record GetPayPeriodByIdQuery(Guid Id) : IRequest<Result<PayPeriodDto>>;

public sealed class GetPayPeriodByIdQueryHandler(
    Abstractions.IPayrollRepository payrollRepository,
    ICurrentUser currentUser) : IRequestHandler<GetPayPeriodByIdQuery, Result<PayPeriodDto>>
{
    public async Task<Result<PayPeriodDto>> Handle(GetPayPeriodByIdQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Dispatcher)
        {
            return Error.Forbidden("forbidden", "Only administrators or dispatchers can view pay period details.");
        }

        var period = await payrollRepository.GetPayPeriodByIdAsync(request.Id, cancellationToken);
        if (period is null)
        {
            return Error.NotFound("pay_period_not_found", $"Pay period with ID '{request.Id}' was not found.");
        }

        var payslips = await payrollRepository.GetPayslipsByPeriodIdAsync(period.Id, cancellationToken);
        return PayPeriodDto.FromEntity(period, payslips.Count);
    }
}
