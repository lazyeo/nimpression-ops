namespace Nimpression.Application.Common.Security;

/// <summary>
/// JWT 访问令牌与刷新令牌生成接口。
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// 生成 15 分钟时效的 JWT Access Token。
    /// </summary>
    (string Token, int ExpiresInSeconds) GenerateAccessToken(Guid userId, string email, string role, string displayName);

    /// <summary>
    /// 生成 7 天时效的 Cryptographically Secure Refresh Token 及其中间哈希值。
    /// </summary>
    (string RawToken, string TokenHash, DateTimeOffset ExpiresAt) GenerateRefreshToken(string? ipAddress);

    /// <summary>
    /// 计算 Refresh Token 原始字符串的安全哈希，用于数据库索引比对。
    /// </summary>
    string HashRefreshToken(string rawToken);
}
