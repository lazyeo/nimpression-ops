namespace Nimpression.Application.Common.Abstractions;

/// <summary>
/// 审计事件的落地口。定义在应用层、实现在基础设施层，
/// 好让 <see cref="Behaviors.AuditBehavior{TRequest,TResponse}"/> 不必知道 EF 的存在。
/// </summary>
public interface IAuditSink
{
    /// <summary>
    /// 写入一条审计。**必须与业务变更同事务** —— 否则业务回滚后审计仍在，
    /// 审计表就成了"发生过但其实没发生"的记录。
    /// </summary>
    Task RecordAsync(
        string entityType,
        Guid? entityId,
        string action,
        string? beforeJson,
        string? afterJson,
        CancellationToken cancellationToken = default);
}
