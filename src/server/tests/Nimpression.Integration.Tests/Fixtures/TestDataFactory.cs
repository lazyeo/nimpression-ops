using Nimpression.Domain.ValueObjects;

namespace Nimpression.Integration.Tests.Fixtures;

/// <summary>
/// 集成测试共享数据工厂：统一管理测试实体数据生成，保障数据隔离，彻底杜绝唯一约束碰撞。
/// </summary>
public static class TestDataFactory
{
    public const string DefaultDomain = "nimpression.co.nz";

    private static long _emailCounter;
    private static long _driverCounter;
    private static long _regoCounter;

    private static readonly char[] Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    /// <summary>
    /// 生成全局唯一且包含单调序列的测试邮箱（格式: {prefix}_{guid:N}_{counter}@{domain}）。
    /// </summary>
    public static string CreateEmail(string prefix = "test")
    {
        var seq = Interlocked.Increment(ref _emailCounter);
        return $"{prefix}_{Guid.NewGuid():N}_{seq}@{DefaultDomain}";
    }

    /// <summary>
    /// 生成全局唯一的 EmailAddress 值对象。
    /// </summary>
    public static EmailAddress CreateEmailAddress(string prefix = "test") =>
        new(CreateEmail(prefix));

    /// <summary>
    /// 生成全局唯一且单调递增的司机工号（格式: {prefix}-{counter:D6}，例如 DRV-000001）。
    /// 长度为 10~15 字符（满足 DB MaxLength(30) 约束），线程安全单调自增，100% 确定性无碰撞，
    /// 且不会与种子数据中的预置工号（DRV-001 ~ DRV-010）产生任何冲突。
    /// </summary>
    public static string CreateEmployeeNo(string prefix = "DRV")
    {
        var seq = Interlocked.Increment(ref _driverCounter);
        return $"{prefix}-{seq:D6}".ToUpperInvariant();
    }

    /// <summary>
    /// 生成符合新西兰车牌格式 (1-6 位大写英数字符 ^[A-Z0-9]{1,6}$) 且全局确定性递增唯一的车牌号。
    /// 采用 Base36 编码将进程内原子计数器转换为紧凑字符串（例如前缀 'T' + 5 位 Base36 支持超过 6000 万种唯一组合），
    /// 完全杜绝随机数在生日悖论下导致的偶发唯一键冲突。
    /// </summary>
    public static string CreateRego(string prefix = "T")
    {
        var seq = Interlocked.Increment(ref _regoCounter);
        var remainingLen = 6 - prefix.Length;
        var base36 = ToBase36(seq, Math.Max(1, remainingLen));
        if (base36.Length > remainingLen)
        {
            base36 = base36[^remainingLen..];
        }
        return $"{prefix}{base36}".ToUpperInvariant();
    }

    /// <summary>
    /// 生成全局唯一的 Rego 值对象。
    /// </summary>
    public static Rego CreateRegoObject(string prefix = "T") =>
        new(CreateRego(prefix));

    private static string ToBase36(long value, int minLength)
    {
        var buffer = new char[12];
        var pos = buffer.Length;
        do
        {
            buffer[--pos] = Base36Chars[value % 36];
            value /= 36;
        } while (value > 0);

        var result = new string(buffer, pos, buffer.Length - pos);
        return result.Length < minLength ? result.PadLeft(minLength, '0') : result;
    }
}
