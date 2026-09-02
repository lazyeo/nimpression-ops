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
        List<User> users,
        DateOnly? baseDate = null)
    {
        var today = baseDate ?? SeedConstants.ReferenceDate;
        var vehicles = new List<Vehicle>();
        var assignments = new List<VehicleAssignment>();
        var readings = new List<OdometerReading>();
        var dispatcherUser = users.First(u => u.Role == UserRole.Dispatcher);

        // 11 Vehicles with comprehensive, dynamic WOF, COF and Insurance expiry samples:
        // - WOF: Expired (NIM008: today-12d), 30d window (NIM005: 7d, NIM006: 14d, NIM007: 28d), Normal (NIM001-NIM004, NIM009-NIM011: >30d)
        // - COF: Expired (NIM004: today-10d), 30d window (NIM002: 28d, NIM003: 14d, NIM005: 7d), Normal (NIM001, NIM006-NIM010: >30d)
        // - Insurance: Expired (NIM009: today-15d), 30d window (NIM006: 14d, NIM007: 7d, NIM008: 28d), Normal (NIM001-NIM005, NIM010-NIM011: >30d)
        var vehicleConfigs = new (string Rego, string Make, string Model, int Year, decimal Odo, decimal SvcInterval, decimal LastSvc, DateOnly? Wof, DateOnly? Cof, DateOnly? Ins, VehicleStatus Status)[]
        {
            // 1. 正常车 (WOF 正常, COF 正常, 保险正常)
            ("NIM001", "Isuzu", "NPR 450", 2021, 85400m, 10000m, 80000m, today.AddDays(180), today.AddDays(180), today.AddDays(180), VehicleStatus.Active),
            // 2. COF 30天窗口到期 (28天) (WOF 正常, 保险正常)
            ("NIM002", "Isuzu", "FTR 850", 2020, 125000m, 15000m, 120000m, today.AddDays(200), today.AddDays(28), today.AddDays(200), VehicleStatus.Active),
            // 3. 达到保养阈值 (DistanceSinceLastService 10500 >= 10000 -> F3.4) + COF 14天窗口 (WOF 正常, 保险正常)
            ("NIM003", "Hino", "500 Series", 2019, 160500m, 10000m, 150000m, today.AddDays(150), today.AddDays(14), today.AddDays(150), VehicleStatus.Active),
            // 4. COF 已过期 (10天前) (WOF 正常, 保险正常)
            ("NIM004", "Fuso", "Canter 515", 2018, 192000m, 10000m, 190000m, today.AddDays(120), today.AddDays(-10), today.AddDays(120), VehicleStatus.Maintenance),
            // 5. WOF 7天窗口 + COF 7天窗口 (保险正常)
            ("NIM005", "Isuzu", "NQR 500", 2022, 62000m, 10000m, 60000m, today.AddDays(7), today.AddDays(7), today.AddDays(220), VehicleStatus.Active),
            // 6. WOF 14天窗口 + 保险 14天窗口 (COF 正常)
            ("NIM006", "Hino", "300 Series", 2023, 38000m, 10000m, 30000m, today.AddDays(14), today.AddDays(240), today.AddDays(14), VehicleStatus.Active),
            // 7. WOF 30天窗口 (28天) + 保险 7天窗口 (COF 正常)
            ("NIM007", "Fuso", "Fighter 1024", 2021, 98000m, 15000m, 90000m, today.AddDays(28), today.AddDays(210), today.AddDays(7), VehicleStatus.Active),
            // 8. WOF 已过期 (12天前) + 保险 30天窗口 (28天) (COF 正常)
            ("NIM008", "Isuzu", "NPR 450", 2022, 54000m, 10000m, 50000m, today.AddDays(-12), today.AddDays(190), today.AddDays(28), VehicleStatus.Maintenance),
            // 9. 保险已过期 (15天前) (WOF 正常, COF 正常)
            ("NIM009", "Mercedes-Benz", "Actros 2653", 2020, 210000m, 20000m, 200000m, today.AddDays(250), today.AddDays(250), today.AddDays(-15), VehicleStatus.Maintenance),
            // 10. 全项正常车 (WOF 正常, COF 正常, 保险正常)
            ("NIM010", "Scania", "R500", 2019, 280000m, 20000m, 270000m, today.AddDays(300), today.AddDays(300), today.AddDays(300), VehicleStatus.Active),
            // 11. 备用轻型皮卡 (WOF 正常, 无 COF, 保险正常)
            ("NIM011", "Isuzu", "D-Max", 2023, 22000m, 10000m, 20000m, today.AddDays(180), null, today.AddDays(180), VehicleStatus.Inactive)
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
