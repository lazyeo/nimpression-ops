using Nimpression.Domain.Common;
using Nimpression.Domain.Enums;
using Nimpression.Domain.Exceptions;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Domain.Entities.Communications;

/// <summary>
/// 外部伙伴联系人聚合根（保险公司、维修保养厂、年检机构）。
/// </summary>
public sealed class PartnerContact : AggregateRoot
{
    public PartnerKind Kind { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public EmailAddress Email { get; private set; }
    public bool Active { get; private set; }

    private PartnerContact()
    {
    }

    public PartnerContact(
        Guid id,
        PartnerKind kind,
        string companyName,
        EmailAddress email,
        bool active = true) : base(id)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new DomainValidationException("Company name cannot be empty.");
        }

        Kind = kind;
        CompanyName = companyName.Trim();
        Email = email;
        Active = active;
    }

    public void UpdateDetails(PartnerKind kind, string companyName, EmailAddress email)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new DomainValidationException("Company name cannot be empty.");
        }

        Kind = kind;
        CompanyName = companyName.Trim();
        Email = email;
    }

    public void Activate() => Active = true;
    public void Deactivate() => Active = false;
}
