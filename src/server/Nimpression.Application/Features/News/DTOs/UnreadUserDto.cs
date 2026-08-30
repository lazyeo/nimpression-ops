using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.News.DTOs;

/// <summary>
/// 未读人员信息 DTO。
/// </summary>
public sealed record UnreadUserDto(
    Guid UserId,
    string DisplayName,
    string Email,
    UserRole Role,
    string? EmployeeNo);
