using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class UserDriverSeeder
{
    public static (List<User> Users, List<Driver> Drivers) Generate(string? passwordHash = null)
    {
        var hash = passwordHash ?? SeedConstants.DefaultPasswordHash;
        var users = new List<User>();
        var drivers = new List<Driver>();

        // 1 Admin
        var adminUser = new User(
            new Guid("10000000-0000-0000-0000-000000000001"),
            new EmailAddress("admin@nimpression.co.nz"),
            hash,
            UserRole.Admin,
            "Sarah Connor",
            "en-NZ",
            SeedConstants.ReferenceNow.AddDays(-120));
        users.Add(adminUser);

        // 2 Dispatchers
        var dispatcher1 = new User(
            new Guid("10000000-0000-0000-0000-000000000002"),
            new EmailAddress("dispatch.north@nimpression.co.nz"),
            hash,
            UserRole.Dispatcher,
            "Dave Miller",
            "en-NZ",
            SeedConstants.ReferenceNow.AddDays(-110));
        users.Add(dispatcher1);

        var dispatcher2 = new User(
            new Guid("10000000-0000-0000-0000-000000000003"),
            new EmailAddress("dispatch.south@nimpression.co.nz"),
            hash,
            UserRole.Dispatcher,
            "Emma Watson",
            "en-NZ",
            SeedConstants.ReferenceNow.AddDays(-100));
        users.Add(dispatcher2);

        // 10 Drivers
        var driverProfiles = new[]
        {
            ("Liam Smith", "liam.smith@nimpression.co.nz", "DRV-001", "Class 4", new DateOnly(2027, 5, 20), 32.50m, 45.00m, 0.85m, new DateOnly(2024, 1, 15)),
            ("Noah Jones", "noah.jones@nimpression.co.nz", "DRV-002", "Class 4", new DateOnly(2027, 8, 14), 31.00m, 42.00m, 0.80m, new DateOnly(2024, 3, 1)),
            ("Oliver Williams", "oliver.williams@nimpression.co.nz", "DRV-003", "Class 5", new DateOnly(2028, 1, 10), 35.00m, 50.00m, 0.95m, new DateOnly(2023, 6, 12)),
            ("Jack Brown", "jack.brown@nimpression.co.nz", "DRV-004", "Class 2", new DateOnly(2026, 9, 15), 28.50m, 38.00m, 0.75m, new DateOnly(2024, 6, 1)), // 30天内驾照到期
            ("Leo Davis", "leo.davis@nimpression.co.nz", "DRV-005", "Class 4", new DateOnly(2027, 11, 30), 30.00m, 40.00m, 0.80m, new DateOnly(2024, 2, 10)),
            ("Lucas Wilson", "lucas.wilson@nimpression.co.nz", "DRV-006", "Class 5", new DateOnly(2028, 4, 22), 36.00m, 52.00m, 1.00m, new DateOnly(2023, 9, 20)),
            ("Mason Taylor", "mason.taylor@nimpression.co.nz", "DRV-007", "Class 2", new DateOnly(2027, 3, 18), 29.00m, 39.00m, 0.75m, new DateOnly(2024, 5, 5)),
            ("Ethan Anderson", "ethan.anderson@nimpression.co.nz", "DRV-008", "Class 4", new DateOnly(2027, 7, 25), 33.00m, 46.00m, 0.88m, new DateOnly(2023, 11, 1)),
            ("Alexander Thomas", "alexander.thomas@nimpression.co.nz", "DRV-009", "Class 2", new DateOnly(2027, 9, 10), 22.00m, 35.00m, 0.70m, new DateOnly(2024, 4, 15)), // 低于最低时薪保底测试驱动
            ("James Jackson", "james.jackson@nimpression.co.nz", "DRV-010", "Class 4", new DateOnly(2027, 12, 5), 32.00m, 44.00m, 0.85m, new DateOnly(2024, 1, 20))
        };

        for (var i = 0; i < driverProfiles.Length; i++)
        {
            var p = driverProfiles[i];
            var userId = new Guid($"20000000-0000-0000-0000-{i + 1:D12}");
            var driverId = new Guid($"30000000-0000-0000-0000-{i + 1:D12}");

            var user = new User(
                userId,
                new EmailAddress(p.Item2),
                hash,
                UserRole.Driver,
                p.Item1,
                "en-NZ",
                SeedConstants.ReferenceNow.AddDays(-100 + i));
            users.Add(user);

            var driver = new Driver(
                driverId,
                userId,
                p.Item3,
                p.Item4,
                p.Item5,
                new Money(p.Item6),
                new Money(p.Item7),
                new Money(p.Item8),
                $"ENC(phone_+6421{100000 + i})",
                $"ENC(addr_{10 + i}_Queen_Street_Auckland)",
                $"ENC(emg_+64219999{i:D2})",
                p.Item9,
                DriverStatus.Active);
            drivers.Add(driver);
        }

        return (users, drivers);
    }
}
