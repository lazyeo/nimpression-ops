using System.Text.Json.Serialization;
using Nimpression.Domain.Services.Payroll;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Payroll;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DriverTaxProfileStatus { Pending = 1, Approved = 2, Rejected = 3, Withdrawn = 4 }

public sealed record EmployeeTaxDeclaration(string? TaxCode, KiwiSaverEmployeeConfiguration? KiwiSaverEmployee);
public sealed record DriverTaxDeclaration(SettlementWorkerType? WorkerType, EmployeeTaxDeclaration? Employee, ContractorSettlementProfile? Contractor);

/// <summary>A driver declaration is a version, not an editable shared payroll setting.</summary>
public sealed class DriverTaxProfile
{
    public Guid Id { get; private set; }
    public Guid DriverId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DriverTaxProfileStatus Status { get; private set; }
    public DriverTaxDeclaration Declaration { get; private set; } = null!;
    public EmployeeSettlementProfile? ApprovedEmployee { get; private set; }
    public ContractorSettlementProfile? ApprovedContractor { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public Guid SubmittedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    private DriverTaxProfile() { }
    public DriverTaxProfile(Guid id, Guid driverId, DateOnly effectiveFrom, DriverTaxDeclaration declaration, DateTimeOffset submittedAt, Guid submittedBy)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (id == Guid.Empty || driverId == Guid.Empty || submittedBy == Guid.Empty)
            throw new DomainValidationException("A tax declaration requires valid identifiers.");
        var issues = PayrollSettlementCalculator.ValidateDeclaration(effectiveFrom, declaration.WorkerType,
            declaration.Employee?.TaxCode, declaration.Employee?.KiwiSaverEmployee, declaration.Employee is not null, declaration.Contractor);
        if (issues.Count != 0) throw new DomainValidationException("The tax declaration is incomplete or unsupported.");
        Id = id; DriverId = driverId; EffectiveFrom = effectiveFrom; Declaration = declaration;
        SubmittedAt = submittedAt; SubmittedBy = submittedBy; Status = DriverTaxProfileStatus.Pending;
    }
    public void Approve(DateOnly effectiveFrom, KiwiSaverEmployerConfiguration? employer, HolidayPayConfiguration? holiday, DateTimeOffset at, Guid actor)
    {
        EnsurePending();
        if (effectiveFrom < EffectiveFrom) throw new DomainValidationException("Approval cannot bring forward the driver's requested effective date.");
        EnsureReviewActor(at, actor);
        if (Declaration.WorkerType == SettlementWorkerType.Contractor && (employer is not null || holiday is not null))
            throw new DomainValidationException("Contractors cannot use employee contribution settings.");
        var approvedEmployee = Declaration.Employee is { } employee
            ? new EmployeeSettlementProfile(employee.TaxCode, employee.KiwiSaverEmployee, employer, holiday) : null;
        var validation = PayrollSettlementCalculator.Calculate(new(effectiveFrom, SettlementPayFrequency.Weekly, 0,
            Declaration.WorkerType, approvedEmployee, Declaration.Contractor));
        if (!validation.IsSuccess) throw new DomainValidationException("Complete verified employer settings before approval.");
        EffectiveFrom = effectiveFrom;
        ApprovedEmployee = approvedEmployee;
        ApprovedContractor = Declaration.Contractor;
        Review(DriverTaxProfileStatus.Approved, at, actor);
    }
    public void Reject(DateTimeOffset at, Guid actor) { EnsurePending(); Review(DriverTaxProfileStatus.Rejected, at, actor); }
    public void Withdraw(DateTimeOffset at, Guid actor) { EnsurePending(); Review(DriverTaxProfileStatus.Withdrawn, at, actor); }
    private void EnsurePending()
    {
        if (Status != DriverTaxProfileStatus.Pending) throw new DomainValidationException("Only pending tax declarations can be reviewed or withdrawn.");
    }
    private void Review(DriverTaxProfileStatus status, DateTimeOffset at, Guid actor)
    { EnsureReviewActor(at, actor); Status = status; ReviewedAt = at; ReviewedBy = actor; }
    private void EnsureReviewActor(DateTimeOffset at, Guid actor)
    {
        if (actor == Guid.Empty || at < SubmittedAt) throw new DomainValidationException("A review requires a valid actor and a time on or after submission.");
    }
}
