namespace Nimpression.Infrastructure.Persistence.Seed;

public static class SeedConstants
{
    public const int DefaultSeed = 42;
    public const string DefaultPassword = "Passw0rd!demo"; // allow-hardcoded: deterministic demo seed password for local development
    // BCrypt hash for "Passw0rd!demo" with workFactor 12 (deterministic pre-computed hash for fast and reproducible seeding)
    public const string DefaultPasswordHash = "$2a$12$kfTj4/YHot0LwVZvNGaAl.4v.eWwD/xRaL6T/3l6kFTt/uij7LL0O"; // allow-hardcoded: deterministic demo seed password hash for local development seeding

    public static readonly DateTimeOffset ReferenceNow = new(2026, 8, 23, 12, 0, 0, TimeSpan.FromHours(12));
    public static readonly DateOnly ReferenceDate = new(2026, 8, 23);
    public static readonly DateOnly HistoryStartDate = ReferenceDate.AddDays(-90);
}
