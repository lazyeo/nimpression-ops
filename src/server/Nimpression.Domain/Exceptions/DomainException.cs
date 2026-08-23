namespace Nimpression.Domain.Exceptions;

/// <summary>
/// 领域异常基类。所有领域规则违反和领域状态错误均继承此类。
/// </summary>
public class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
