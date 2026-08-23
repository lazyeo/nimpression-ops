namespace Nimpression.Application.Features.Identity.DTOs;

public sealed record LoginResultDto(
    string AccessToken,
    int ExpiresIn,
    string TokenType,
    string RawRefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserDto User);
