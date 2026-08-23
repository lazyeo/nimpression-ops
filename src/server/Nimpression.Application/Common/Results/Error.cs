namespace Nimpression.Application.Common.Results;

/// <summary>
/// 失败的分类。映射到 HTTP 状态码时**必须区分 NotFound 与 Forbidden**：
/// 对"存在但无权访问"的资源返回 404 会泄露资源存在性，
/// 但 F7.10 / N1.3 明确要求返回 403 —— 因为司机能不能看别人的工资单
/// 本身不是秘密，把它伪装成"不存在"反而让越权行为难以审计。
/// </summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unauthorized,
    /// <summary>业务规则不满足。对应 422，与 400（请求格式错误）区分开。</summary>
    UnprocessableEntity,
    TooManyRequests,
    /// <summary>不支持的媒体类型。对应 415（F2.2）。</summary>
    UnsupportedMediaType,
}

/// <summary>
/// 一条可翻译的失败信息。<paramref name="Code"/> 是稳定的机器可读键，
/// 前端据此做 i18n（F13.3），不依赖 <paramref name="Message"/> 的具体措辞。
/// </summary>
public sealed record Error(ErrorKind Kind, string Code, string Message, IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null)
        => new(ErrorKind.Validation, code, message, details);

    public static Error NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static Error Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);

    public static Error Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);

    public static Error Unauthorized(string code, string message) => new(ErrorKind.Unauthorized, code, message);

    public static Error Unprocessable(string code, string message) => new(ErrorKind.UnprocessableEntity, code, message);

    public static Error TooManyRequests(string code, string message) => new(ErrorKind.TooManyRequests, code, message);

    public static Error UnsupportedMediaType(string code, string message) => new(ErrorKind.UnsupportedMediaType, code, message);
}
