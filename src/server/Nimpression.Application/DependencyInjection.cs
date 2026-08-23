using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Common.Behaviors;

namespace Nimpression.Application;

/// <summary>
/// 应用层的组合根。全部走程序集扫描注册 ——
/// 新增一个用例切片只需新增文件，**不需要回来改这里**。
/// 这是让多个开发者（或多个 agent）并行开发不同切片而不产生
/// 合并冲突的前提。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationAssemblyMarker).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // 顺序即执行顺序，且是有意为之：
            // 日志最外层（失败也要记）→ 校验（挡住脏数据，省掉后面的开销）
            // → 事务（handler 与审计必须在同一个事务里）→ 审计（贴着 handler，只审成功的变更）
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuditBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
