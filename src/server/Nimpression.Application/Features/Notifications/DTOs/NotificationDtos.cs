using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Notifications.DTOs;

/// <summary>
/// 外部伙伴联系人数据传输对象（F11.1）。
/// </summary>
public sealed record PartnerContactDto(
    Guid Id,
    PartnerKind Kind,
    string CompanyName,
    string Email,
    bool Active);

/// <summary>
/// 伙伴联系人列表筛选条件。
/// </summary>
public sealed record PartnerContactFilter(
    PartnerKind? Kind = null,
    bool? Active = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// 邮件模板数据传输对象（F11.2）。
/// </summary>
public sealed record EmailTemplateDto(
    Guid Id,
    string Key,
    string SubjectEn,
    string SubjectZh,
    string BodyEn,
    string BodyZh,
    bool Active);

/// <summary>
/// 邮件模板列表筛选条件。
/// </summary>
public sealed record EmailTemplateFilter(
    string? SearchTerm = null,
    bool? Active = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// 邮件发送日志数据传输对象（F11.5）。
/// </summary>
public sealed record EmailLogDto(
    Guid Id,
    string TemplateKey,
    string ToAddress,
    string Subject,
    string Status,
    int Attempts,
    string? LastError,
    DateTimeOffset? SentAt,
    string TriggeredBy,
    string CorrelationId);

/// <summary>
/// 邮件日志列表筛选条件。
/// </summary>
public sealed record EmailLogFilter(
    string? Status = null,
    string? TemplateKey = null,
    string? ToAddress = null,
    string? CorrelationId = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// 通用分页包装对象。
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(1, PageSize));
}
