using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Features.Realtime.Abstractions;
using Nimpression.Infrastructure.Realtime.BackgroundServices;
using Nimpression.Infrastructure.Realtime.Persistence;
using Nimpression.Infrastructure.Realtime.Security;
using Nimpression.Infrastructure.Realtime.Services;

namespace Nimpression.Infrastructure.Realtime;

/// <summary>
/// 实时通信基础设施层服务注册扩展方法。
/// </summary>
public static class RealtimeInfrastructureExtensions
{
    public static IServiceCollection AddRealtimeInfrastructure(this IServiceCollection services)
    {
        services.ConfigureOptions<RealtimeJwtBearerOptionsSetup>();
        services.AddSingleton<IOutboxToRealtimeMapper, OutboxToRealtimeMapper>();
        services.AddScoped<IRealtimeChangesRepository, RealtimeChangesRepository>();
        services.AddHostedService<OutboxProcessorBackgroundService>();

        return services;
    }
}
