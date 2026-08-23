using Nimpression.Domain.Enums;

namespace Nimpression.Domain.Exceptions;

/// <summary>
/// 罚单非法状态跃迁异常。
/// </summary>
public sealed class InvalidFineTransitionException : DomainException
{
    public FineStatus From { get; }
    public FineStatus To { get; }

    public InvalidFineTransitionException(FineStatus from, FineStatus to)
        : base($"Invalid Fine status transition from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }
}
