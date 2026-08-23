using Nimpression.Domain.Common;
using Nimpression.Domain.Exceptions;

namespace Nimpression.Domain.Entities.Communications;

/// <summary>
/// 邮件模板聚合根。支持中英双语与占位符规范。
/// </summary>
public sealed class EmailTemplate : AggregateRoot
{
    public string Key { get; private set; } = string.Empty;
    public string SubjectEn { get; private set; } = string.Empty;
    public string SubjectZh { get; private set; } = string.Empty;
    public string BodyEn { get; private set; } = string.Empty;
    public string BodyZh { get; private set; } = string.Empty;
    public bool Active { get; private set; }

    private EmailTemplate()
    {
    }

    public EmailTemplate(
        Guid id,
        string key,
        string subjectEn,
        string subjectZh,
        string bodyEn,
        string bodyZh,
        bool active = true) : base(id)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainValidationException("Template key cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(subjectEn) && string.IsNullOrWhiteSpace(subjectZh))
        {
            throw new DomainValidationException("At least one language subject must be provided.");
        }

        if (string.IsNullOrWhiteSpace(bodyEn) && string.IsNullOrWhiteSpace(bodyZh))
        {
            throw new DomainValidationException("At least one language body must be provided.");
        }

        Key = key.Trim().ToUpperInvariant();
        SubjectEn = subjectEn.Trim();
        SubjectZh = subjectZh.Trim();
        BodyEn = bodyEn.Trim();
        BodyZh = bodyZh.Trim();
        Active = active;
    }

    public void UpdateContent(string subjectEn, string subjectZh, string bodyEn, string bodyZh)
    {
        if (string.IsNullOrWhiteSpace(subjectEn) && string.IsNullOrWhiteSpace(subjectZh))
        {
            throw new DomainValidationException("At least one language subject must be provided.");
        }

        if (string.IsNullOrWhiteSpace(bodyEn) && string.IsNullOrWhiteSpace(bodyZh))
        {
            throw new DomainValidationException("At least one language body must be provided.");
        }

        SubjectEn = subjectEn.Trim();
        SubjectZh = subjectZh.Trim();
        BodyEn = bodyEn.Trim();
        BodyZh = bodyZh.Trim();
    }

    public void Activate() => Active = true;
    public void Deactivate() => Active = false;
}
