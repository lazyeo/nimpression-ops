namespace Nimpression.Application.Common.Abstractions;

/// <summary>
/// 标记"写"请求。管道据此决定是否开事务 —— 查询不该拿写锁。
/// 用显式标记而非按命名约定（以 Command 结尾）判断，
/// 因为命名笔误会静默地让一条写命令失去事务保护。
/// </summary>
public interface ICommandMarker;
