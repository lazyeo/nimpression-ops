using Nimpression.Domain.Services.Payroll;

namespace Nimpression.Domain.Entities.Payroll;

/// <summary>Verified calculation inputs and results frozen with the payslip, independent of later profile changes.</summary>
public sealed record PayslipSettlementSnapshot(
    SettlementRequest Request,
    SettlementCalculation Calculation,
    string RulesVersion,
    DateTimeOffset CalculatedAt,
    bool IsDemo = false);
