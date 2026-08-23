using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class VehicleSeeder
{
    public static (List<Vehicle> Vehicles, List<VehicleAssignment> Assignments, List<OdometerReading> Readings) Generate(
        List<Driver> drivers,
        List<User> users)
    {
        var vehicles = new List<Vehicle>();
        var assignments = new List<VehicleAssignment>();
        var readings = new List<OdometerReading>();
        var dispatcherUser = users.First(u => u.Role == UserRole.Dispatcher);

        // 11 Vehicles
        var vehicleConfigs = new (string Rego, string Make, string Model, int Year, decimal Odo, decimal SvcInterval, decimal LastSvc, DateOnly? Wof, DateOnly? Cof, DateOnly? Ins, VehicleStatus Status)[]
        {
            // 1. 正常车 (COF 正常, 保险正常)
            ("NIM001", "Isuzu", "NPR 450", 2021, 85400m, 10000m, 80000m, null, new DateOnly(2027, 2, 15), new DateOnly(2027, 3, 1), VehicleStatus.Active),
            // 2. 正常车
            ("NIM002", "Isuzu", "FTR 850", 2020, 125000m, 15000m, 120000m, null, new DateOnly(2027, 4, 10), new DateOnly(2027, 5, 1), VehicleStatus.Active),
            // 3. 达到保养阈值 (DistanceSinceLastService 10500 >= 10000, 触发 F3.4 / ServiceThresholdReached)
            ("NIM003", "Hino", "500 Series", 2019, 160500m, 10000m, 150000m, null, new DateOnly(2027, 1, 20), new DateOnly(2027, 2, 15), VehicleStatus.Active),
            // 4. COF 已到期 (到期日 2026-08-10 < 2026-08-23, 用于合规拦截测试)
            ("NIM004", "Fuso", "Canter 515", 2018, 192000m, 10000m, 190000m, null, new DateOnly(2026, 8, 10), new DateOnly(2026, 12, 31), VehicleStatus.Maintenance),
            // 5. 30天内到期 (COF 到期日 2026-09-08, 触发 F3.5 预警)
            ("NIM005", "Isuzu", "NQR 500", 2022, 62000m, 10000m, 60000m, null, new DateOnly(2026, 9, 8), new DateOnly(2027, 1, 15), VehicleStatus.Active),
            // 6. 保险 30 天内到期 (2026-09-12)
            ("NIM006", "Hino", "300 Series", 2023, 38000m, 10000m, 30000m, null, new DateOnly(2027, 6, 30), new DateOnly(2026, 9, 12), VehicleStatus.Active),
            // 7. 正常车
            ("NIM007", "Fuso", "Fighter 1024", 2021, 98000m, 15000m, 90000m, null, new DateOnly(2027, 7, 14), new DateOnly(2027, 8, 1), VehicleStatus.Active),
            // 8. 正常车
            ("NIM008", "Isuzu", "NPR 450", 2022, 54000m, 10000m, 50000m, null, new DateOnly(2027, 5, 25), new DateOnly(2027, 6, 1), VehicleStatus.Active),
            // 9. 正常车
            ("NIM009", "Mercedes-Benz", "Actros 2653", 2020, 210000m, 20000m, 200000m, null, new DateOnly(2027, 8, 30), new DateOnly(2027, 9, 1), VehicleStatus.Active),
            // 10. 正常车
            ("NIM010", "Scania", "R500", 2019, 280000m, 20000m, 270000m, null, new DateOnly(2027, 3, 10), new DateOnly(2027, 4, 1), VehicleStatus.Active),
            // 11. 备用/闲置车
            ("NIM011", "Isuzu", "D-Max", 2023, 22000m, 10000m, 20000m, new DateOnly(2027, 9, 1), null, new DateOnly(2027, 10, 1), VehicleStatus.Inactive)
        };

        for (var i = 0; i < vehicleConfigs.Length; i++)
        {
            var c = vehicleConfigs[i];
            var vehicleId = new Guid($"40000000-0000-0000-0000-{i + 1:D12}");

            var vehicle = new Vehicle(
                vehicleId,
                new Rego(c.Rego),
                c.Make,
                c.Model,
                c.Year,
                $"ENC(VIN_7AT00NZ{100000 + i})",
                new Kilometres(c.Odo),
                new Kilometres(c.SvcInterval),
                new Kilometres(c.LastSvc),
                c.Wof,
                c.Cof,
                c.Ins,
                c.Status);
            vehicles.Add(vehicle);

            // 为前 10 台车分配 10 名司机
            if (i < 10 && i < drivers.Count)
            {
                // 历史已释放分派
                var pastAssignment = new VehicleAssignment(
                    new Guid($"50000000-0000-0000-0000-{i * 2 + 1:D12}"),
                    vehicleId,
                    drivers[i].Id,
                    SeedConstants.ReferenceNow.AddDays(-80),
                    dispatcherUser.Id);
                pastAssignment.Release(SeedConstants.ReferenceNow.AddDays(-40));
                assignments.Add(pastAssignment);

                // 当前生效中未释放分派 (ReleasedAt = null)
                var currentAssignment = new VehicleAssignment(
                    new Guid($"50000000-0000-0000-0000-{i * 2 + 2:D12}"),
                    vehicleId,
                    drivers[i].Id,
                    SeedConstants.ReferenceNow.AddDays(-39),
                    dispatcherUser.Id);
                assignments.Add(currentAssignment);

                // 里程表读数历史
                var reading = new OdometerReading(
                    new Guid($"60000000-0000-0000-0000-{i + 1:D12}"),
                    vehicleId,
                    drivers[i].Id,
                    new Kilometres(c.Odo),
                    $"odometer/photo_{c.Rego}_{c.Odo}.jpg",
                    SeedConstants.ReferenceNow.AddHours(-i * 3));
                readings.Add(reading);
            }
        }

        return (vehicles, assignments, readings);
    }
}
