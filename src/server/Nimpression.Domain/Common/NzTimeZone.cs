namespace Nimpression.Domain.Common;

/// <summary>
/// 新西兰时区（Pacific/Auckland）助手，支持跨平台（Linux/macOS IANA ID 与 Windows ID 兼容）。
/// </summary>
public static class NzTimeZone
{
    private static readonly Lazy<TimeZoneInfo> LazyInfo = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        }
    });

    /// <summary>
    /// 新西兰时区 TimeZoneInfo 实例。
    /// </summary>
    public static TimeZoneInfo Info => LazyInfo.Value;

    /// <summary>
    /// 将 UTC 或任意 DateTimeOffset 转换为新西兰本地 DateTimeOffset。
    /// </summary>
    public static DateTimeOffset ToNzDateTimeOffset(DateTimeOffset dto)
    {
        return TimeZoneInfo.ConvertTime(dto, Info);
    }

    /// <summary>
    /// 获取指定 DateTimeOffset 对应的新西兰本地日历日期（DateOnly）。
    /// </summary>
    public static DateOnly ToNzDateOnly(DateTimeOffset dto)
    {
        var local = ToNzDateTimeOffset(dto);
        return DateOnly.FromDateTime(local.DateTime);
    }
}
