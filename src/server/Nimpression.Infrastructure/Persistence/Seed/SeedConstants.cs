namespace Nimpression.Infrastructure.Persistence.Seed;

public static class SeedConstants
{
    public const int DefaultSeed = 42;
    public const string DefaultPassword = "Passw0rd!demo";
    // BCrypt hash for "Passw0rd!demo" with workFactor 12 (deterministic pre-computed hash for fast and reproducible seeding)
    public const string DefaultPasswordHash = "$2a$12$4mU3Z8oQc4eX1m2R7T9X4.P1O8vW7Y2K6N3L9M8J5H4G3F2E1D0C.";

    public static readonly DateTimeOffset ReferenceNow = new(2026, 8, 23, 12, 0, 0, TimeSpan.FromHours(12));
    public static readonly DateOnly ReferenceDate = new(2026, 8, 23);
    public static readonly DateOnly HistoryStartDate = ReferenceDate.AddDays(-90);
}
