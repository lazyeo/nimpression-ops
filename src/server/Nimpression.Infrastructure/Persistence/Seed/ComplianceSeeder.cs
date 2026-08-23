using Nimpression.Domain.Entities.Compliance;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class ComplianceSeeder
{
    public static (List<Fine> Fines, List<IncidentReport> Incidents) Generate(
        List<Driver> drivers,
        List<Vehicle> vehicles,
        List<User> users,
        int randomSeed = SeedConstants.DefaultSeed)
    {
        var rng = new Random(randomSeed);
        var fines = new List<Fine>();
        var incidents = new List<IncidentReport>();
        var admin = users.First(u => u.Role == UserRole.Admin);

        var fineAuthorities = new[] { "NZ Police", "Auckland Transport", "NZTA Waka Kotahi" };
        var fineReasons = new[]
        {
            "Exceeding 50 km/h speed limit in urban zone (58 km/h detected)",
            "Bus lane infringement during peak restriction hours",
            "Failure to display valid COF label on windscreen",
            "Stopping in clearway zone during designated hours",
            "Unauthorised parking in commercial loading zone"
        };

        // 生成 12 条罚单记录，涵盖各种状态
        for (var i = 0; i < 12; i++)
        {
            var driver = drivers[i % drivers.Count];
            var vehicle = vehicles[i % Math.Min(10, vehicles.Count)];
            var issuedDate = SeedConstants.ReferenceDate.AddDays(-rng.Next(5, 80));
            var amount = new Money(rng.Next(80, 400));
            var fineId = new Guid($"B0000000-0000-0000-0000-{i + 1:D12}");

            var fine = new Fine(
                fineId,
                driver.Id,
                vehicle.Id,
                issuedDate,
                fineAuthorities[i % fineAuthorities.Length],
                $"INF-{issuedDate:yyyyMM}-{1000 + i}",
                amount,
                fineReasons[i % fineReasons.Length],
                $"fines/ticket_{i + 1}.jpg");

            var statusRoll = i % 5;
            var reviewTime = new DateTimeOffset(issuedDate.Year, issuedDate.Month, issuedDate.Day, 14, 0, 0, TimeSpan.FromHours(12)).AddDays(2);

            switch (statusRoll)
            {
                case 0:
                    // Submitted (新提交，未审核)
                    break;
                case 1:
                    // UnderReview
                    fine.StartReview(admin.Id, reviewTime);
                    break;
                case 2:
                    // Accepted
                    fine.StartReview(admin.Id, reviewTime);
                    fine.Accept(admin.Id, reviewTime.AddHours(2), "Driver acknowledged liability; fine forwarded to accounts.");
                    break;
                case 3:
                    // Disputed
                    fine.StartReview(admin.Id, reviewTime);
                    fine.Dispute(admin.Id, reviewTime.AddHours(2), "Disputing speed detection calibration per GPS telemetry logs.");
                    break;
                case 4:
                    // Waived
                    fine.StartReview(admin.Id, reviewTime);
                    fine.Waive(admin.Id, reviewTime.AddHours(2), "Auckland Transport approved waiver due to road signage obstruction.");
                    break;
            }

            fines.Add(fine);
        }

        // 生成 6 条事故报告
        var incidentConfigs = new (int DaysAgo, IncidentSeverity Severity, string Loc, string Desc, bool NotifiedInsurer)[]
        {
            (75, IncidentSeverity.Minor, "Great South Road / Penrose, Auckland", "Minor scrape on rear left bumper while reversing into tight depot loading bay.", false),
            (60, IncidentSeverity.Moderate, "State Highway 1 near Highbrook interchange", "Rear-ended third party vehicle during abrupt highway deceleration in heavy rain.", true),
            (45, IncidentSeverity.Minor, "Albany Highway, North Shore", "Side mirror clipped low-hanging tree branch in suburban delivery area.", false),
            (30, IncidentSeverity.Major, "Southern Motorway near Drury off-ramp", "Multi-vehicle collision in wet weather conditions resulting in front cab panel damage.", true),
            (18, IncidentSeverity.Moderate, "Ti Rakau Drive, East Tamaki", "Sideswipe with parked van on narrow industrial road during freight drop.", true),
            (5, IncidentSeverity.Minor, "Customs Street East, Auckland CBD", "Tailgate scraped loading dock bumper rubber guard.", false)
        };

        for (var i = 0; i < incidentConfigs.Length; i++)
        {
            var cfg = incidentConfigs[i];
            var driver = drivers[i % drivers.Count];
            var vehicle = vehicles[i % Math.Min(10, vehicles.Count)];
            var incidentId = new Guid($"C0000000-0000-0000-0000-{i + 1:D12}");
            var occurredAt = SeedConstants.ReferenceNow.AddDays(-cfg.DaysAgo);

            var incident = new IncidentReport(
                incidentId,
                driver.Id,
                vehicle.Id,
                occurredAt,
                cfg.Loc,
                cfg.Severity,
                cfg.Desc,
                new[] { $"incidents/inc_{i + 1}_photo1.jpg", $"incidents/inc_{i + 1}_photo2.jpg" },
                $"ENC(ThirdParty_Rego_ABC{100 + i}_Name_John_Doe_Ph_+6421555{i:D3})",
                "Processed");

            if (cfg.NotifiedInsurer)
            {
                incident.MarkInsurerNotified(occurredAt.AddHours(4));
            }

            incidents.Add(incident);
        }

        return (fines, incidents);
    }
}
