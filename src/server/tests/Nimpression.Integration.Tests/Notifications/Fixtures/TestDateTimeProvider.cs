using Nimpression.Application.Common.Abstractions;

namespace Nimpression.Integration.Tests.Notifications.Fixtures;

/// <summary>
/// 测试用确定性时钟提供者（支持任意时间推进与边界模拟）。
/// </summary>
public sealed class TestDateTimeProvider : IDateTimeProvider
{
    private DateTimeOffset _utcNow;
    private DateOnly _nzToday;

    public TestDateTimeProvider(DateTimeOffset initialUtcNow, DateOnly initialNzToday)
    {
        _utcNow = initialUtcNow;
        _nzToday = initialNzToday;
    }

    public static TestDateTimeProvider FromNzDate(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var utc = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.FromHours(12)).ToUniversalTime();
        return new TestDateTimeProvider(utc, date);
    }

    public DateTimeOffset UtcNow => _utcNow;

    public DateTimeOffset NzNow => _utcNow.ToOffset(TimeSpan.FromHours(12));

    public DateOnly NzToday => _nzToday;

    public void AdvanceTime(TimeSpan delta)
    {
        _utcNow = _utcNow.Add(delta);
    }

    public void SetNzToday(DateOnly newDate)
    {
        _nzToday = newDate;
        _utcNow = new DateTimeOffset(newDate.Year, newDate.Month, newDate.Day, 0, 0, 0, TimeSpan.FromHours(12)).ToUniversalTime();
    }

    public void SetUtcNow(DateTimeOffset newUtcNow)
    {
        _utcNow = newUtcNow;
    }
}
