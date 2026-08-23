using System.Reflection;

namespace Nimpression.Domain.Tests.Architecture;

/// <summary>
/// 守护 Clean Architecture 的依赖方向。这些约束靠约定维持不住 ——
/// 只要有人在 Domain 里 <c>using Microsoft.EntityFrameworkCore</c> 图省事，
/// 分层就名存实亡了，而且这种腐化在 code review 里极难察觉。
/// 放在测试里，越界即红。
/// </summary>
public sealed class LayerDependencyTests
{
    /// <summary>Domain 层绝对不允许依赖的基础设施与框架程序集前缀。</summary>
    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "MediatR",
        "Microsoft.AspNetCore",
        "Serilog",
        "FluentValidation",
        "Swashbuckle",
        "AWSSDK",
    ];

    [Fact]
    public void Domain_has_no_infrastructure_or_framework_dependencies()
    {
        var domain = typeof(DomainAssemblyMarker).Assembly;

        var violations = domain
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => ForbiddenInDomain.Any(
                forbidden => name.StartsWith(forbidden, StringComparison.Ordinal)))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"Nimpression.Domain 引用了不允许的程序集: {string.Join(", ", violations)}. " +
            "领域层必须保持对框架无知；把这些依赖挪到 Application 或 Infrastructure。");
    }

    [Fact]
    public void Domain_references_only_the_base_class_library()
    {
        var domain = typeof(DomainAssemblyMarker).Assembly;

        var nonBcl = domain
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                        && !name.Equals("netstandard", StringComparison.Ordinal)
                        && !name.Equals("mscorlib", StringComparison.Ordinal))
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            nonBcl.Length == 0,
            $"Nimpression.Domain 只应依赖 BCL，但发现: {string.Join(", ", nonBcl)}");
    }
}
