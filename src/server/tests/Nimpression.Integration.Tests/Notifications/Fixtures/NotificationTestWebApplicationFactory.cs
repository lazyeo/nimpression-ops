using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Integration.Tests.Notifications.Fixtures;

/// <summary>
/// 通知模块集成测试专用的 WebApplicationFactory。
/// 支持重定向数据库连接与可注入的确定性时钟。
/// </summary>
public sealed class NotificationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly IDateTimeProvider? _customDateTimeProvider;

    public NotificationTestWebApplicationFactory(
        string connectionString,
        IDateTimeProvider? customDateTimeProvider = null)
    {
        _connectionString = connectionString;
        _customDateTimeProvider = customDateTimeProvider;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
            });

            if (_customDateTimeProvider != null)
            {
                var dtDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDateTimeProvider));
                if (dtDescriptor != null)
                {
                    services.Remove(dtDescriptor);
                }
                services.AddSingleton(_customDateTimeProvider);
            }
        });
    }
}
