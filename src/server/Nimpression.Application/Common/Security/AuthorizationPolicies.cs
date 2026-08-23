namespace Nimpression.Application.Common.Security;

/// <summary>
/// 授权策略名称常量。
/// 集中定义策略名称常量，避免在控制器/端点和配置中使用魔法字符串（字面量）。
/// 若使用字面量，策略名拼错时在编译期无法发现，运行期要么导致合法请求被拒，要么导致端点根本未受保护（或回退失效），两者均无编译告警。
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// 仅系统管理员（Admin）可访问。
    /// </summary>
    public const string AdminOnly = "policy:admin-only";

    /// <summary>
    /// 调度员或管理员（Admin 或 Dispatcher）可访问。
    /// </summary>
    public const string Dispatcher = "policy:dispatcher";

    /// <summary>
    /// 任何已认证的用户可访问。
    /// </summary>
    public const string AuthenticatedUser = "policy:authenticated";

    /// <summary>
    /// 仅司机（Driver）可访问。
    /// </summary>
    public const string DriverOnly = "policy:driver-only";
}
