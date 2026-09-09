using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Enums;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;
using Nimpression.Integration.Tests.Fixtures;
using Npgsql;

namespace Nimpression.Integration.Tests.Persistence;

[Collection("PostgreSqlCollection")]
public sealed class DemoContactRepairTests(PostgreSqlContainerFixture fixture)
{
    [Fact]
    public async Task Repair_OnlyChangesExactLegacySeedFields_AndIsIdempotent()
    {
        // Separate disposable database so fixed demo IDs never modify the shared fixture's records.
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Database = $"contact_repair_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options;
        var otherDriverId = Guid.NewGuid();
        try
        {
            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.MigrateAsync();
                var (users, drivers) = UserDriverSeeder.Generate();
                for (var i = 0; i < drivers.Count; i++)
                {
                    drivers[i].UpdateEncryptedContactInfo(
                        $"ENC(phone_+6421{100000 + i})",
                        $"ENC(addr_{10 + i}_Queen_Street_Auckland)",
                        $"ENC(emg_+64219999{i:D2})");
                }
                drivers[1].UpdateEncryptedContactInfo("ENC(+64215550123)", drivers[1].AddressEnc, drivers[1].EmergencyContactEnc);
                // Similar-looking text is not an exact legacy seed match.
                drivers[2].UpdateEncryptedContactInfo("ENC(phone_+6421555555)", "ENC(44 Custom Road, Wellington)", "ENC(Family contact: +64215550234)");
                setup.Users.AddRange(users);
                setup.Drivers.AddRange(drivers);

                var otherUser = new User(Guid.NewGuid(), TestDataFactory.CreateEmailAddress("nonseed_contact"),
                    "test-hash", UserRole.Driver, "Non-seed driver", "en-NZ", SeedConstants.ReferenceNow);
                var sample = drivers[0];
                setup.Users.Add(otherUser);
                setup.Drivers.Add(new Driver(
                    otherDriverId, otherUser.Id, TestDataFactory.CreateEmployeeNo(), sample.LicenceClass,
                    sample.LicenceExpiry, sample.HourlyRate, sample.PerTripRate, sample.PerKmRate,
                    sample.PhoneEnc, sample.AddressEnc, sample.EmergencyContactEnc, sample.HiredOn));
                await setup.SaveChangesAsync();
            }

            await using (var repair = new AppDbContext(options))
            {
                (await UserDriverSeeder.RepairLegacyContactsAsync(repair)).Should().Be(26);
                (await UserDriverSeeder.RepairLegacyContactsAsync(repair)).Should().Be(0);
            }

            await using var verify = new AppDbContext(options);
            var first = await verify.Drivers.SingleAsync(driver => driver.Id == new Guid("30000000-0000-0000-0000-000000000001"));
            first.PhoneEnc.Should().Be("ENC(+6421100000)");
            first.AddressEnc.Should().Be("ENC(10 Queen Street, Auckland)");
            first.EmergencyContactEnc.Should().Be("ENC(Emergency contact: +6421999900)");
            var edited = await verify.Drivers.SingleAsync(driver => driver.Id == new Guid("30000000-0000-0000-0000-000000000002"));
            edited.PhoneEnc.Should().Be("ENC(+64215550123)");
            edited.AddressEnc.Should().Be("ENC(11 Queen Street, Auckland)");
            var similar = await verify.Drivers.SingleAsync(driver => driver.Id == new Guid("30000000-0000-0000-0000-000000000003"));
            similar.PhoneEnc.Should().Be("ENC(phone_+6421555555)");
            similar.AddressEnc.Should().Be("ENC(44 Custom Road, Wellington)");
            similar.EmergencyContactEnc.Should().Be("ENC(Family contact: +64215550234)");
            var unchanged = await verify.Drivers.SingleAsync(driver => driver.Id == otherDriverId);
            unchanged.PhoneEnc.Should().Be("ENC(phone_+6421100000)");
            unchanged.AddressEnc.Should().Be("ENC(addr_10_Queen_Street_Auckland)");
            unchanged.EmergencyContactEnc.Should().Be("ENC(emg_+6421999900)");
            (await verify.Drivers.CountAsync()).Should().Be(11);
        }
        finally
        {
            await using var cleanup = new AppDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
