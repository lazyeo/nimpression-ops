using Microsoft.EntityFrameworkCore;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Port=5432;Database=nimpression;Username=nimpression;Password=devonly_change_me";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    });
});

var app = builder.Build();

if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var summary = await DatabaseSeeder.SeedAsync(dbContext, cleanExisting: false);
    Console.WriteLine($"Database seeding completed successfully. Users: {summary.UsersCount}, Drivers: {summary.DriversCount}, Vehicles: {summary.VehiclesCount}, Areas: {summary.AreasCount}, Tasks: {summary.JobTasksCount}, Shifts: {summary.ShiftEntriesCount}, PayPeriods: {summary.PayPeriodsCount}, Payslips: {summary.PayslipsCount}.");
    return;
}

app.MapGet("/", () => "Hello World!");

app.Run();
