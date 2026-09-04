using System.Text.Json.Serialization;
using Nimpression.Api.Endpoints;
using Nimpression.Application;
using Nimpression.Infrastructure;
using Nimpression.Infrastructure.Diagnostics;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Migrations;
using Nimpression.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddSignalR();
builder.Services.AddScoped<Nimpression.Application.Features.Realtime.Abstractions.IRealtimeNotifier, Nimpression.Api.Hubs.RealtimeNotifier>();

var app = builder.Build();

// 迁移模式：不启动 Web 服务器，执行完数据库迁移即退出。
// 生产环境通过独立子命令执行迁移，支持容器编排的 Pre-deploy / Init 任务。
if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseMigrator.MigrateAsync(dbContext);
        Console.WriteLine("Database migration completed successfully.");
        return;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Database migration failed: {ex.Message}");
        Console.Error.WriteLine(ex.ToString());
        Environment.Exit(1);
        return;
    }
}

// 种子模式：不启动 Web 服务器，灌完数据即退出。
// 放在 Build 之后、中间件之前，好复用完整的 DI 容器。
if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var summary = await DatabaseSeeder.SeedAsync(dbContext, cleanExisting: false);
    Console.WriteLine(
        $"Database seeding completed successfully. Users: {summary.UsersCount}, Drivers: {summary.DriversCount}, " +
        $"Vehicles: {summary.VehiclesCount}, Areas: {summary.AreasCount}, Tasks: {summary.JobTasksCount}, " +
        $"Shifts: {summary.ShiftEntriesCount}, PayPeriods: {summary.PayPeriodsCount}, Payslips: {summary.PayslipsCount}.");
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", async (AppDbContext dbContext, CancellationToken ct) =>
{
    var (isHealthy, details) = await DatabaseSchemaHealthCheck.CheckAsync(dbContext, ct);
    if (!isHealthy)
    {
        return Results.Json(new
        {
            status = "unhealthy",
            database = "unhealthy",
            reason = details
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "healthy",
        database = "healthy"
    });
})
   .WithName("HealthCheck")
   .WithTags("Diagnostics");

// 全部功能端点在此自动挂载。新增一组端点请实现 IEndpointModule，
// 不要在这里加行 —— 见 Endpoints/IEndpointModule.cs 的说明。
app.MapEndpointModules(typeof(Program).Assembly);

app.MapHub<Nimpression.Api.Hubs.RealtimeHub>("/hubs/realtime");

app.Run();

/// <summary>
/// 供 <c>WebApplicationFactory&lt;Program&gt;</c> 在集成测试里引用（N4.4）。
/// Minimal API 的隐式 Program 类是 internal，必须显式公开。
/// </summary>
public partial class Program;
