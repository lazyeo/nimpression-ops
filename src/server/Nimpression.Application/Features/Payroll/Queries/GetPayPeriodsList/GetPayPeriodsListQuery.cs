using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.Queries.GetPayPeriodsList;

public sealed record GetPayPeriodsListQuery(PayPeriodFilter Filter) : IRequest<Result<PagedResult<PayPeriodDto>>>;

public sealed class GetPayPeriodsListQueryHandler(
    Abstractions.IPayrollRepository payrollRepository,
    ICurrentUser currentUser) : IRequestHandler<GetPayPeriodsListQuery, Result<PagedResult<PayPeriodDto>>>
{
    public async Task<Result<PagedResult<PayPeriodDto>>> Handle(GetPayPeriodsListQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Dispatcher)
        {
            return Error.Forbidden("forbidden", "Only administrators or dispatchers can list pay periods.");
        }

        var result = await payrollRepository.GetPayPeriodsListAsync(request.Filter, cancellationToken);
        return result;
    }
}
