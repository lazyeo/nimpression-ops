using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Privacy.DTOs;

/// <summary>
/// 隐私主体请求 DTO（查阅导出 / 匿名化删除 / 更正）。
/// </summary>
public sealed record DataSubjectRequestDto(
    Guid Id,
    Guid SubjectUserId,
    DataSubjectRequestKind Kind,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string? ExportKey,
    string? RejectionReason);
