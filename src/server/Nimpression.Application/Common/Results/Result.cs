namespace Nimpression.Application.Common.Results;

/// <summary>
/// 显式的成功/失败载体。用它而非抛异常表达**预期内**的失败
/// （找不到、无权限、冲突），异常只留给真正意外的情况。
/// 这样 handler 的失败路径在签名上可见，不会被悄悄忽略。
/// </summary>
public readonly record struct Result
{
    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

/// <inheritdoc cref="Result"/>
public readonly record struct Result<TValue>
{
    private Result(TValue? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    private readonly TValue? _value;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    /// <summary>失败时读取会抛出 —— 强制调用方先检查 <see cref="IsSuccess"/>。</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value of a failed result ({Error!.Code}).");

    public static Result<TValue> Success(TValue value) => new(value, null);

    public static Result<TValue> Failure(Error error) => new(default, error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
