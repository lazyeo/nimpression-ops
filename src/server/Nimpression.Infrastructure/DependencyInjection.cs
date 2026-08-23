using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Infrastructure.Persistence;

namespace Nimpression.Infrastructure;

/// <summary>
/// 基础设施层的组合根。与应用层同理：新增一个适配器实现只需新增文件，
/// 具体注册尽量收敛到本文件的分组方法内，避免 Program.cs 变成人人必改的热点。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = ResolveConnectionString(configuration);

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                // 多个 Include 时拆成多条查询，避免笛卡尔积把列表接口的
                // 返回行数放大若干倍（N3.6 无 N+1 的另一面：也别搞成一次巨查询）
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
        });

        services.AddHttpContextAccessor();
        services.AddScoped<Nimpression.Application.Common.Abstractions.ICurrentUser, Nimpression.Infrastructure.Security.CurrentUser>();
        services.AddScoped<Nimpression.Application.Common.Abstractions.IUnitOfWork, Nimpression.Infrastructure.Persistence.UnitOfWork>();
        services.AddScoped<Nimpression.Application.Common.Abstractions.IAuditSink, Nimpression.Infrastructure.Persistence.Auditing.AuditSink>();

        return services;
    }

    /// <summary>
    /// 连接串解析顺序：配置 → 标准环境变量 → 容器编排常见的 DATABASE_URL → 本地默认值。
    /// 本地默认值只含开发口令，生产靠环境变量覆盖（N1.7：无密钥入库）。
    /// </summary>
    public static string ResolveConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=nimpression;Username=nimpression;Password=devonly_change_me";
    }
}
