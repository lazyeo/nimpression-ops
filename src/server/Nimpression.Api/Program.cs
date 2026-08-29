using Nimpression.Api.Endpoints;
using Nimpression.Application;
using Nimpression.Infrastructure;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddScoped<Nimpression.Application.Features.Realtime.Abstractions.IRealtimeNotifier, Nimpression.Api.Hubs.RealtimeNotifier>();

var app = builder.Build();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
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
