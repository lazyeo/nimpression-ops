using System.Reflection;

namespace Nimpression.Api.Endpoints;

/// <summary>
/// <see cref="IEndpointModule"/> 的自动发现与挂载。
/// </summary>
public static class EndpointModuleExtensions
{
    /// <summary>
    /// 扫描程序集里全部 <see cref="IEndpointModule"/> 实现并逐个挂载。
    /// 按类型名排序只是为了让路由注册顺序稳定、便于比对启动日志，
    /// 端点之间不应存在顺序依赖。
    /// </summary>
    public static IEndpointRouteBuilder MapEndpointModules(this IEndpointRouteBuilder routes, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(assembly);

        var moduleTypes = assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointModule).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (var moduleType in moduleTypes)
        {
            var module = (IEndpointModule)Activator.CreateInstance(moduleType)!;
            module.MapEndpoints(routes);
        }

        return routes;
    }
}
