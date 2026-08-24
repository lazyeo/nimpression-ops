namespace Nimpression.Application.Common.Security;

/// <summary>
/// 密码哈希与校验接口。
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 使用 BCrypt（workFactor >= 12）对明文密码进行单向哈希。
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// 校验明文密码与哈希值是否匹配。
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);
}
