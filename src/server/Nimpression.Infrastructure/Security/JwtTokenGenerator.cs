using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Security;

namespace Nimpression.Infrastructure.Security;

/// <summary>
/// JWT 访问令牌与刷新令牌生成器。
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;
    private readonly IDateTimeProvider? _dateTimeProvider;

    public JwtTokenGenerator(
        IOptions<JwtSettings> jwtOptions,
        IDateTimeProvider? dateTimeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);
        _settings = jwtOptions.Value;
        _settings.Validate();
        _dateTimeProvider = dateTimeProvider;
    }

    public (string Token, int ExpiresInSeconds) GenerateAccessToken(
        Guid userId,
        string email,
        string role,
        string displayName)
    {
        var now = _dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_settings.AccessTokenLifetimeMinutes);
        var expiresInSeconds = (int)(expiresAt - now).TotalSeconds;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("role", role),
            new(JwtRegisteredClaimNames.Name, displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return (tokenString, expiresInSeconds);
    }

    public (string RawToken, string TokenHash, DateTimeOffset ExpiresAt) GenerateRefreshToken(string? ipAddress)
    {
        var now = _dateTimeProvider?.UtcNow ?? DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(_settings.RefreshTokenLifetimeDays);

        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        var rawToken = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var tokenHash = HashRefreshToken(rawToken);

        return (rawToken, tokenHash, expiresAt);
    }

    public string HashRefreshToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
