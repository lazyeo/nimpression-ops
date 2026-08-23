using Nimpression.Domain.Entities.Area;
using Nimpression.Domain.Entities.Dispatch;
using Nimpression.Domain.Entities.Driver;
using Nimpression.Domain.Entities.Identity;
using Nimpression.Domain.Entities.Vehicle;
using Nimpression.Domain.Enums;
using Nimpression.Domain.ValueObjects;

namespace Nimpression.Infrastructure.Persistence.Seed;

public static class DispatchSeeder
{
    public static List<JobTask> Generate(
        List<Area> areas,
        List<Driver> drivers,
        List<Vehicle> vehicles,
        List<User> users,
        int randomSeed = SeedConstants.DefaultSeed)
    {
        var rng = new Random(randomSeed);
        var tasks = new List<JobTask>();
        var dispatcher = users.First(u => u.Role == UserRole.Dispatcher);

        var taskTitles = new[]
        {
            "Supermarket Pallet Freight Delivery",
            "Express Courier Distribution Run",
            "Bulk Construction Materials Haulage",
            "Port Container Wharf Transfer",
            "Cold Chain Perishable Food Delivery",
            "Industrial Hardware Distribution",
            "Hospitality Supplier Route Delivery",
            "Airport Cargo Clearance Transit"
        };

        var taskIdCounter = 1;

        // 生成过去 90 天的任务
        for (var dayOffset = 90; dayOffset >= 0; dayOffset--)
        {
            var date = SeedConstants.ReferenceDate.AddDays(-dayOffset);
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var jobsToday = isWeekend ? rng.Next(2, 5) : rng.Next(6, 12);

            for (var j = 0; j < jobsToday; j++)
            {
                var driver = drivers[rng.Next(drivers.Count)];
                var vehicle = vehicles[rng.Next(Math.Min(10, vehicles.Count))];
                var area = areas[rng.Next(areas.Count)];
                var title = taskTitles[rng.Next(taskTitles.Length)];

                var hour = rng.Next(6, 18);
                var minute = rng.Next(0, 4) * 15;
                var scheduledTime = new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.FromHours(12));
                var plannedDistance = new Kilometres(rng.Next(15, 120) + (decimal)rng.Next(0, 100) / 100m);

                var taskId = new Guid($"90000000-0000-0000-0000-{taskIdCounter:D12}");
                var refCode = $"TSK-{date:yyyyMMdd}-{taskIdCounter:D4}";
                taskIdCounter++;

                var task = new JobTask(
                    taskId,
                    refCode,
                    title,
                    area.Id,
                    scheduledTime,
                    dispatcher.Id,
                    $"Scheduled dispatch run for {area.Name}",
                    (TaskPriority)rng.Next(1, 4),
                    plannedDistance,
                    driver.Id,
                    vehicle.Id);

                // 状态分配：过去日期的任务大部分为 Completed，小部分 Cancelled；最近 1-2 天有 InProgress/Assigned/Draft
                if (dayOffset > 2)
                {
                    if (rng.Next(100) < 6) // 6% Cancelled
                    {
                        task.Assign(driver.Id, vehicle.Id, scheduledTime);
                        task.Cancel("Customer cancelled delivery slot", scheduledTime.AddMinutes(30));
                    }
                    else // Completed
                    {
                        var startOdo = new Kilometres(rng.Next(40000, 150000));
                        var actualKm = new Kilometres(plannedDistance.Value + (decimal)rng.Next(-5, 10));
                        var endOdo = startOdo + actualKm;

                        task.Assign(driver.Id, vehicle.Id, scheduledTime);
                        task.Acknowledge(scheduledTime.AddMinutes(5));
                        task.Start(scheduledTime.AddMinutes(15), startOdo);
                        task.Complete(scheduledTime.AddMinutes(rng.Next(90, 240)), actualKm, endOdo);
                    }
                }
                else if (dayOffset == 2)
                {
                    var startOdo = new Kilometres(80000m);
                    var actualKm = plannedDistance;
                    task.Assign(driver.Id, vehicle.Id, scheduledTime);
                    task.Acknowledge(scheduledTime.AddMinutes(5));
                    task.Start(scheduledTime.AddMinutes(15), startOdo);
                    task.Complete(scheduledTime.AddHours(2), actualKm, startOdo + actualKm);
                }
                else if (dayOffset == 1)
                {
                    task.Assign(driver.Id, vehicle.Id, scheduledTime);
                    task.Acknowledge(scheduledTime.AddMinutes(5));
                    task.Start(scheduledTime.AddMinutes(10), new Kilometres(95000m));
                }
                else // Today (Day 0)
                {
                    var roll = rng.Next(100);
                    if (roll < 40)
                    {
                        task.Assign(driver.Id, vehicle.Id, scheduledTime);
                    }
                    else if (roll < 70)
                    {
                        task.Assign(driver.Id, vehicle.Id, scheduledTime);
                        task.Acknowledge(scheduledTime.AddMinutes(5));
                    }
                    // else Draft
                }

                tasks.Add(task);
            }
        }

        return tasks;
    }
}
