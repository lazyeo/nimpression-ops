using Nimpression.Domain.Enums;

namespace Nimpression.Application.Features.Identity.DTOs;

public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    string Locale,
    string? AvatarKey);
