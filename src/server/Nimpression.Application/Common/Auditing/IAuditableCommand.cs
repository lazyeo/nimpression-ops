namespace Nimpression.Application.Common.Auditing;

/// <summary>
/// 标记一条命令需要写审计（N1.1）。由 <see cref="Behaviors.AuditBehavior{TRequest,TResponse}"/> 拦截。
/// 用显式标记而非"所有写操作自动审计"，是因为并非每条命令都值得留痕
/// （例如刷新令牌），而漏审计比多审计更危险 —— 所以标记缺失时
/// 架构测试会点名未实现本接口的 Command，强制作者做出选择。
/// </summary>
public interface IAuditableCommand
{
    /// <summary>被操作实体的类型名，例如 "Driver"。</summary>
    string AuditEntityType { get; }

    /// <summary>被操作实体的 Id。创建类命令在执行前未知，可返回 null，由 Behavior 从结果中回填。</summary>
    Guid? AuditEntityId { get; }

    /// <summary>动作名，例如 "DeactivateDriver"。</summary>
    string AuditAction { get; }
}
