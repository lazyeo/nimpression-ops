namespace Nimpression.Domain.Exceptions;

/// <summary>
/// 领域模型或值对象校验失败时抛出的异常。
/// </summary>
public class DomainValidationException : DomainException
{
    public DomainValidationException()
    {
    }

    public DomainValidationException(string message) : base(message)
    {
    }

    public DomainValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
