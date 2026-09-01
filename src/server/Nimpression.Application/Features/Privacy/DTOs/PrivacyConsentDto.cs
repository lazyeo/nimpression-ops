namespace Nimpression.Application.Features.Privacy.DTOs;

/// <summary>
/// 隐私告知与同意状态（AC N2.7）。
/// 包含隐私声明版本、当前用户是否已同意、同意时间戳与完整合规告知文本。
/// </summary>
public sealed record PrivacyConsentDto(
    Guid UserId,
    string PolicyVersion,
    bool HasConsented,
    DateTimeOffset? ConsentedAt,
    string? ConsentedIpAddress,
    string Title,
    string Summary,
    string ContentMarkdown);
