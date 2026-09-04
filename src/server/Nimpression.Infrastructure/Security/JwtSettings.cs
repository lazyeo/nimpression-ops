using System.Text;

namespace Nimpression.Infrastructure.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "nimpression";
    public string Audience { get; set; } = "nimpression-api";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 7;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Secret))
        {
            throw new InvalidOperationException(
                "JWT signing secret is missing. Please configure the 'Jwt__Secret' (or 'Jwt:Secret') environment variable or configuration key.");
        }

        var byteCount = Encoding.UTF8.GetByteCount(Secret);
        if (byteCount < 32)
        {
            throw new InvalidOperationException(
                $"JWT signing secret in 'Jwt__Secret' (or 'Jwt:Secret') must be at least 32 bytes (256 bits) long, but got {byteCount} bytes.");
        }
    }
}

