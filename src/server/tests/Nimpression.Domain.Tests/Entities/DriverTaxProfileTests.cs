using Nimpression.Domain.Entities.Payroll;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.Services.Payroll;

namespace Nimpression.Domain.Tests.Entities;

public class DriverTaxProfileTests
{
    private static readonly DateTimeOffset SubmittedAt = new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
    private static DriverTaxProfile Create() => new(Guid.NewGuid(), Guid.NewGuid(), new(2026, 9, 12),
        new(SettlementWorkerType.Employee, new("M", new(0.035m, true, false)), null), SubmittedAt, Guid.NewGuid());

    [Fact]
    public void ApproveRequiresEmployerDeclarations_AndPreservesPersonalVersion()
    {
        var profile = Create();
        var invalid = () => profile.Approve(new(2026, 9, 12), null, null, SubmittedAt.AddHours(1), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(invalid);
        Assert.Equal(DriverTaxProfileStatus.Pending, profile.Status);
        profile.Approve(new(2026, 9, 13), new(0.035m, true, false, 0.30m, true),
            new(HolidayPayMode.OrdinaryAccrual, false, false), SubmittedAt.AddHours(1), Guid.NewGuid());
        Assert.Equal("M", profile.ApprovedEmployee!.TaxCode);
        Assert.Equal(profile.Declaration.Employee!.KiwiSaverEmployee, profile.ApprovedEmployee.KiwiSaverEmployee);
        Assert.Equal(new DateOnly(2026, 9, 13), profile.EffectiveFrom);
        var withdraw = () => profile.Withdraw(SubmittedAt.AddHours(2), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(withdraw);
        Assert.Equal(DriverTaxProfileStatus.Approved, profile.Status);
    }

    [Fact]
    public void ApprovalCannotBringForwardTheDriverRequestedDate()
    {
        var profile = Create();
        var approve = () => profile.Approve(new(2026, 9, 11), new(0.035m, true, false, 0.30m, true),
            new(HolidayPayMode.OrdinaryAccrual, false, false), SubmittedAt.AddHours(1), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(approve);
        Assert.Equal(DriverTaxProfileStatus.Pending, profile.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosedDeclarationCannotBeReused(bool withdraw)
    {
        var profile = Create();
        if (withdraw) profile.Withdraw(SubmittedAt.AddHours(1), Guid.NewGuid());
        else profile.Reject(SubmittedAt.AddHours(1), Guid.NewGuid());
        var approve = () => profile.Approve(new(2026, 9, 13), new(0.035m, true, false, 0.30m, true),
            new(HolidayPayMode.OrdinaryAccrual, false, false), SubmittedAt.AddHours(2), Guid.NewGuid());
        Assert.Throws<DomainValidationException>(approve);
    }

    [Fact]
    public void PersonalValidationRejectsRawUnknownTaxCode_AndMissingContributionDeclaration()
    {
        Assert.Contains(PayrollSettlementCalculator.ValidateDeclaration(new(2026, 9, 12), SettlementWorkerType.Employee,
            "unexpected input", new(0.035m, true, false), true, null), i => i.Code == "unsupported_tax_code");
        Assert.Contains(PayrollSettlementCalculator.ValidateDeclaration(new(2026, 9, 12), SettlementWorkerType.Employee,
            "M", new(0.035m, null, false), true, null), i => i.Code == "missing_kiwisaver_configuration");
    }
}
