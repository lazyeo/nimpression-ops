using FluentValidation;
using MediatR;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Common.Behaviors;

/// <summary>
/// 在 handler 之前跑完该请求的全部 FluentValidation 校验器（N1.4）。
/// 失败时返回 <see cref="Result"/> 而非抛异常，好让失败以 RFC 9457
/// problem+json 的形式统一返回，且不污染 handler 的正常路径。
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var applicable = validators.ToArray();
        if (applicable.Length == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            applicable.Select(v => v.ValidateAsync(context, cancellationToken))).ConfigureAwait(false);

        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToArray();
        if (failures.Length == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var details = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        var error = Error.Validation("validation_failed", "One or more validation errors occurred.", details);

        return ResultFactory.FailureOf<TResponse>(error);
    }
}
