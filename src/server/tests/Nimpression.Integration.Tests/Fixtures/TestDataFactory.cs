using Nimpression.Domain.ValueObjects;

namespace Nimpression.Integration.Tests.Fixtures;

/// <summary>
/// 集成测试共享数据工厂：统一管理测试实体数据生成，保障数据隔离，防止唯一约束冲突。
/// </summary>
public static class TestDataFactory
{
    public const string DefaultDomain = "nimpression.co.nz";

    /// <summary>
    /// 生成全局唯一的测试邮箱（格式: {prefix}_{guid:N}@{domain}），彻底避免测试间唯一索引冲突。
    /// </summary>
    public static string CreateEmail(string prefix = "test") =>
        $"{prefix}_{Guid.NewGuid():N}@{DefaultDomain}";

    /// <summary>
    /// 生成全局唯一的 EmailAddress 值对象。
    /// </summary>
    public static EmailAddress CreateEmailAddress(string prefix = "test") =>
        new(CreateEmail(prefix));

    /// <summary>
    /// 生成唯一的司机工号（格式: DRV-XXXX，大写）。
    /// </summary>
    public static string CreateEmployeeNo(string prefix = "DRV") =>
        $"{prefix}-{Guid.NewGuid():N}"[..7].ToUpperInvariant();

    /// <summary>
    /// 生成唯一的车牌号（格式: NIMXXXX，大写）。
    /// </summary>
    public static string CreateRego(string prefix = "NIM") =>
        $"{prefix}{Random.Shared.Next(1000, 9999)}".ToUpperInvariant();
}
