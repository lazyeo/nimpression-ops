namespace Nimpression.Application.Common.Abstractions;

/// <summary>
/// 可替换的时钟。工时、薪期、到期提醒的用例都需要在测试里冻结"现在"，
/// 直接调 <c>DateTimeOffset.UtcNow</c> 会让这些用例只能靠真实时间碰运气。
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }

    /// <summary>新西兰当地当前时刻。跨零点班次与 DST 边界的判定以此为准。</summary>
    DateTimeOffset NzNow { get; }

    DateOnly NzToday { get; }
}
