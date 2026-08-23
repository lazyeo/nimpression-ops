using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Driver;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class AreaSeeder
{
    public static (List<Area> Areas, List<AreaAssignment> Assignments) Generate(List<Driver> drivers)
    {
        var areas = new List<Area>();
        var assignments = new List<AreaAssignment>();

        var areaConfigs = new[]
        {
            ("Auckland Central", "AKL-CBD", "Auckland City Centre and immediate commercial hubs", "{\"type\":\"Polygon\",\"coordinates\":[[[174.75,-36.84],[174.77,-36.84],[174.77,-36.86],[174.75,-36.86],[174.75,-36.84]]]}"),
            ("North Shore", "AKL-NS", "Takapuna, Albany, and Northern corridor", "{\"type\":\"Polygon\",\"coordinates\":[[[174.70,-36.70],[174.78,-36.70],[174.78,-36.80],[174.70,-36.80],[174.70,-36.70]]]}"),
            ("West Auckland", "AKL-WEST", "Henderson, Westgate, and New Lynn logistics parks", "{\"type\":\"Polygon\",\"coordinates\":[[[174.58,-36.85],[174.68,-36.85],[174.68,-36.95],[174.58,-36.95],[174.58,-36.85]]]}"),
            ("South Auckland", "AKL-SOUTH", "Manukau, Airport precinct, and industrial corridor", "{\"type\":\"Polygon\",\"coordinates\":[[[174.80,-36.95],[174.95,-36.95],[174.95,-37.10],[174.80,-37.10],[174.80,-36.95]]]}"),
            ("East Auckland", "AKL-EAST", "East Tamaki, Howick, and Highbrook industrial estate", "{\"type\":\"Polygon\",\"coordinates\":[[[174.85,-36.88],[174.95,-36.88],[174.95,-36.96],[174.85,-36.96],[174.85,-36.88]]]}"),
            ("Waikato Express", "WAI-EXP", "Long haul Auckland-Hamilton regional freight lane", "{\"type\":\"Polygon\",\"coordinates\":[[[174.90,-37.10],[175.30,-37.10],[175.30,-37.80],[174.90,-37.80],[174.90,-37.10]]]}")
        };

        for (var i = 0; i < areaConfigs.Length; i++)
        {
            var cfg = areaConfigs[i];
            var areaId = new Guid($"70000000-0000-0000-0000-{i + 1:D12}");

            var area = new Area(
                areaId,
                cfg.Item1,
                cfg.Item2,
                cfg.Item3,
                cfg.Item4,
                true);
            areas.Add(area);
        }

        // 为 10 名司机分配区域
        for (var i = 0; i < drivers.Count; i++)
        {
            var driver = drivers[i];
            var primaryArea = areas[i % areas.Count];
            var secondaryArea = areas[(i + 1) % areas.Count];

            // 主区域分配（生效至今）
            var assign1 = new AreaAssignment(
                new Guid($"80000000-0000-0000-0000-{i * 2 + 1:D12}"),
                primaryArea.Id,
                driver.Id,
                SeedConstants.HistoryStartDate,
                null);
            assignments.Add(assign1);

            // 备用区域分配（部分司机有历史交替分配）
            if (i % 2 == 0)
            {
                var assign2 = new AreaAssignment(
                    new Guid($"80000000-0000-0000-0000-{i * 2 + 2:D12}"),
                    secondaryArea.Id,
                    driver.Id,
                    SeedConstants.HistoryStartDate.AddDays(30),
                    SeedConstants.HistoryStartDate.AddDays(60));
                assignments.Add(assign2);
            }
        }

        return (areas, assignments);
    }
}
