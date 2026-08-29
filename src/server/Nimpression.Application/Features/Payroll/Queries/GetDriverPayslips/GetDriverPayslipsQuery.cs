using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Payroll.Queries.GetDriverPayslips;

public sealed record GetDriverPayslipsQuery(
    Guid? DriverId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<PayslipDto>>>;

public sealed class GetDriverPayslipsQueryHandler(
    Abstractions.IPayrollRepository payrollRepository,
    ICurrentUser currentUser) : IRequestHandler<GetDriverPayslipsQuery, Result<PagedResult<PayslipDto>>>
{
    public async Task<Result<PagedResult<PayslipDto>>> Handle(GetDriverPayslipsQuery request, CancellationToken cancellationToken)
    {
        Guid targetDriverId;

        if (currentUser.Role == UserRole.Driver)
        {
            var driver = currentUser.UserId.HasValue
                ? await payrollRepository.GetDriverByUserIdAsync(currentUser.UserId.Value, cancellationToken)
                : null;

            if (driver is null)
            {
                return Error.Forbidden("driver_profile_not_found", "Current user is not associated with a driver profile.");
            }

            // F7.10: 司机只能看自己的工资单
            if (request.DriverId.HasValue && request.DriverId.Value != driver.Id)
            {
                return Error.Forbidden("forbidden", "Drivers can only view their own payslips.");
            }

            targetDriverId = driver.Id;
        }
        else if (currentUser.Role == UserRole.Admin || currentUser.Role == UserRole.Dispatcher)
        {
            if (!request.DriverId.HasValue)
            {
                return Error.Validation("driver_id_required", "DriverId is required for admin/dispatcher querying driver payslips.");
            }

            targetDriverId = request.DriverId.Value;
        }
        else
        {
            return Error.Forbidden("forbidden", "Unauthorized to view driver payslips.");
        }

        var filter = new DriverPayslipsFilter(
            DriverId: targetDriverId,
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Page: request.Page,
            PageSize: request.PageSize);

        var result = await payrollRepository.GetPayslipsForDriverPagedAsync(filter, cancellationToken);
        return result;
    }
}
