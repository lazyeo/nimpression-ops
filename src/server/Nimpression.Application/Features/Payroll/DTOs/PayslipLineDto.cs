using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Application.Features.Payroll.DTOs;

public sealed record PayslipLineDto(
    Guid Id,
    PayBasis Basis,
    string Kind,
    string Description,
    decimal Rate,
    string Currency,
    decimal Amount,
    decimal? Hours,
    decimal? Distance,
    int? Qty)
{
    public static PayslipLineDto FromEntity(PayslipLine line) =>
        new(
            line.Id,
            line.Basis,
            line.Kind,
            line.Description,
            line.Rate.Amount,
            line.Rate.Currency,
            line.Amount.Amount,
            line.Hours?.Value,
            line.Distance?.Value,
            line.Qty);
}
