using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nimpression.Infrastructure.Persistence;
using Nimpression.Infrastructure.Realtime.BackgroundServices;

namespace Nimpression.Integration.Tests.Realtime;

public sealed class RealtimeTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly bool _enableBackgroundProcessor;

    public RealtimeTestWebApplicationFactory(string connectionString, bool enableBackgroundProcessor = true)
    {
        _connectionString = connectionString;
        _enableBackgroundProcessor = enableBackgroundProcessor;
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

            if (!_enableBackgroundProcessor)
            {
                var hostedServiceDescriptor = services.FirstOrDefault(d =>
                    d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OutboxProcessorBackgroundService));
                if (hostedServiceDescriptor != null)
                {
                    services.Remove(hostedServiceDescriptor);
                }
            }
        });
    }

    public HubConnection CreateHubConnection(string? token = null, HttpTransportType transports = HttpTransportType.LongPolling)
    {
        var hubUri = new Uri(Server.BaseAddress, "/hubs/realtime");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = transports;
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();

                if (!string.IsNullOrWhiteSpace(token))
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();
    }
}
