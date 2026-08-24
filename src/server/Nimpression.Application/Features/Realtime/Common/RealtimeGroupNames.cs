namespace Nimpression.Application.Features.Realtime.Common;

/// <summary>
/// 实时通信分组命名规则常量与格式化工厂。
/// 严格保证不同角色与司机的隔离通道。
/// </summary>
public static class RealtimeGroupNames
{
    public const string All = "all";

    public static string Driver(Guid driverId) => $"driver:{driverId}";

    public static string User(Guid userId) => $"user:{userId}";

    public static string Role(string role) => $"role:{role}";
}
