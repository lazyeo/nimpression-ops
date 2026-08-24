using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Identity.DTOs;

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    UserStatus Status,
    string Locale,
    string? AvatarKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    bool IsLockedOut);
