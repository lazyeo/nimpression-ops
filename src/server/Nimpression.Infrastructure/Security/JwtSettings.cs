namespace Nimpression.Infrastructure.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = "NimpressionSecureSuperSecretKeyForJwtSigningMustBeAtLeast32BytesLong!";
    public string Issuer { get; set; } = "nimpression";
    public string Audience { get; set; } = "nimpression-api";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 7;
}
