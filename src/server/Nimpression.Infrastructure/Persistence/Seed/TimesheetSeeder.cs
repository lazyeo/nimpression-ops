using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Timesheet;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class TimesheetSeeder
{
    public static List<ShiftEntry> Generate(
        List<Driver> drivers,
        List<Vehicle> vehicles,
        List<User> users,
        int randomSeed = SeedConstants.DefaultSeed)
    {
        var rng = new Random(randomSeed);
        var shifts = new List<ShiftEntry>();
        var adminUser = users.First(u => u.Role == UserRole.Admin);
        var shiftIdCounter = 1;

        // 生成过去 90 天的班次
        for (var dayOffset = 90; dayOffset >= 0; dayOffset--)
        {
            var date = SeedConstants.ReferenceDate.AddDays(-dayOffset);
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            for (var d = 0; d < drivers.Count; d++)
            {
                // 周末只有部分司机上班，工作日 85% 司机上班
                var worksToday = isWeekend ? (rng.Next(100) < 30) : (rng.Next(100) < 85);
                if (!worksToday && dayOffset > 0)
                {
                    continue;
                }

                var driver = drivers[d];
                var vehicle = vehicles[d % Math.Min(10, vehicles.Count)];
                var shiftId = new Guid($"A0000000-0000-0000-0000-{shiftIdCounter:D12}");
                shiftIdCounter++;

                // 区分正常白班与跨零点夜班
                var isNightShift = (d + dayOffset) % 7 == 0; // 规律性跨零点夜班

                DateTimeOffset clockInTime;
                DateTimeOffset? clockOutTime = null;
                int breakMinutes;

                if (isNightShift)
                {
                    // 跨零点夜班：22:00 上班，次日 06:00 下班
                    clockInTime = new DateTimeOffset(date.Year, date.Month, date.Day, 22, 0, 0, TimeSpan.FromHours(12));
                    breakMinutes = 45;
                    if (dayOffset > 0)
                    {
                        clockOutTime = clockInTime.AddHours(8);
                    }
                }
                else
                {
                    // 白班：07:00 ~ 08:30 上班，8 ~ 9.5 小时
                    var startHour = rng.Next(7, 9);
                    var startMin = rng.Next(0, 4) * 15;
                    clockInTime = new DateTimeOffset(date.Year, date.Month, date.Day, startHour, startMin, 0, TimeSpan.FromHours(12));
                    breakMinutes = 30;

                    if (dayOffset > 0)
                    {
                        var durationHours = 8.0 + (rng.Next(-2, 4) * 0.5); // 7.0h ~ 10.0h
                        clockOutTime = clockInTime.AddHours(durationHours);
                    }
                }

                var shift = new ShiftEntry(
                    shiftId,
                    driver.Id,
                    clockInTime,
                    -36.8485m + (decimal)rng.Next(-100, 100) / 10000m,
                    174.7633m + (decimal)rng.Next(-100, 100) / 10000m,
                    vehicle.Id);

                if (clockOutTime.HasValue)
                {
                    shift.ClockOut(
                        clockOutTime.Value,
                        -36.8485m + (decimal)rng.Next(-100, 100) / 10000m,
                        174.7633m + (decimal)rng.Next(-100, 100) / 10000m,
                        breakMinutes,
                        isNightShift ? "Overnight freight logistics shift" : "Standard day route");

                    // 部分班次包含管理员审计更正记录
                    if (shiftIdCounter % 25 == 0)
                    {
                        shift.AdminCorrect(
                            clockInTime.AddMinutes(-15),
                            clockOutTime.Value,
                            breakMinutes,
                            "Corrected start time per GPS depot geofence gate record",
                            adminUser.Id,
                            clockOutTime.Value.AddHours(1));
                    }
                }

                shifts.Add(shift);
            }
        }

        // 额外注入一条显式跨夏令时切换日（2026-04-05 03:00 -> 02:00 回拨）的历史班次供测试验证
        var dstShiftId = new Guid("A0000000-0000-0000-0000-000000009999");
        var dstClockIn = new DateTimeOffset(2026, 4, 4, 22, 0, 0, TimeSpan.FromHours(13));
        var dstClockOut = new DateTimeOffset(2026, 4, 5, 6, 0, 0, TimeSpan.FromHours(12));
        var dstShift = new ShiftEntry(dstShiftId, drivers[0].Id, dstClockIn, -36.8485m, 174.7633m, vehicles[0].Id);
        dstShift.ClockOut(dstClockOut, -36.8485m, 174.7633m, 60, "DST transition night shift");
        shifts.Add(dstShift);

        return shifts;
    }
}
