using Nimpression.Domain.Enums;

namespace Nimpression.Application.Common.Abstractions;

/// <summary>
/// 当前请求的操作者。审计（N1.1）与越权防护（N1.3）都依赖它，
/// 因此它必须由认证中间件填充，而不是由调用方作为参数传入 ——
/// 后者等于把"我是谁"交给不可信的请求体决定。
/// </summary>
public interface ICurrentUser
{
    /// <summary>未认证时为 null。</summary>
    Guid? UserId { get; }

    UserRole? Role { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    bool IsAuthenticated { get; }
}
