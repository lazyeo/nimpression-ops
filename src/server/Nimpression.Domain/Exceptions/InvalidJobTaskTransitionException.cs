using Nimpression.Domain.Enums;

namespace Nimpression.Domain.Exceptions;

/// <summary>
/// 派发任务非法状态跃迁异常。
/// </summary>
public sealed class InvalidJobTaskTransitionException : DomainException
{
    public JobTaskStatus From { get; }
    public JobTaskStatus To { get; }

    public InvalidJobTaskTransitionException(JobTaskStatus from, JobTaskStatus to)
        : base($"Invalid JobTask status transition from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }
}
