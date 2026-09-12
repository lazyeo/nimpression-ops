using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Payroll.DTOs;
using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Services.Payroll;

namespace Nimpression.Application.Features.Payroll.TaxProfiles;

public sealed record DriverTaxProfileDto(Guid Id, Guid DriverId, string? DriverName, string? EmployeeNo,
    DateOnly EffectiveFrom, DriverTaxProfileStatus Status, DriverTaxDeclaration Declaration,
    DateTimeOffset SubmittedAt, DateTimeOffset? ReviewedAt,
    EmployeeSettlementProfile? ApprovedEmployee, ContractorSettlementProfile? ApprovedContractor);
public sealed record SubmitTaxProfileCommand(DateOnly EffectiveFrom, DriverTaxDeclaration? Declaration, bool Confirmed = false)
    : IRequest<Result<DriverTaxProfileDto>>, ICommandMarker;
public sealed record ApproveTaxProfileCommand(Guid Id, DateOnly EffectiveFrom, KiwiSaverEmployerConfiguration? KiwiSaverEmployer,
    HolidayPayConfiguration? HolidayPay, bool Confirmed = false) : IRequest<Result<DriverTaxProfileDto>>, ICommandMarker;
public sealed record CloseTaxProfileCommand(Guid Id, bool Withdraw) : IRequest<Result<DriverTaxProfileDto>>, ICommandMarker;
public sealed record GetTaxProfilesQuery(bool Mine, Guid? DriverId = null, DriverTaxProfileStatus? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<DriverTaxProfileDto>>>;
public sealed record GetPayslipTaxProfileQuery(Guid PayslipId, DateOnly PayDate) : IRequest<Result<DriverTaxProfileDto?>>;
public sealed record SettleFromTaxProfileCommand(Guid PayslipId, DateOnly PayDate, SettlementPayFrequency? Frequency, Guid ProfileId)
    : IRequest<Result<PayslipDto>>, ICommandMarker;

public interface ITaxProfileRepository
{
    Task<Guid?> PayslipDriverIdAsync(Guid payslipId, CancellationToken ct);
    Task LockDriverAsync(Guid driverId, CancellationToken ct);
    Task<DriverTaxProfile?> GetAsync(Guid id, CancellationToken ct);
    Task<DriverTaxProfile?> LatestApprovedAsync(Guid driverId, DateOnly payDate, CancellationToken ct);
    Task<bool> HasPendingAsync(Guid driverId, CancellationToken ct);
    Task<bool> HasApprovedDateAsync(Guid driverId, DateOnly effectiveFrom, CancellationToken ct);
    void Add(DriverTaxProfile profile);
    Task<DriverTaxProfileDto> GetDtoAsync(Guid id, CancellationToken ct);
    Task<PagedResult<DriverTaxProfileDto>> ListAsync(Guid? driverId, DriverTaxProfileStatus? status, int page, int pageSize, CancellationToken ct);
}
