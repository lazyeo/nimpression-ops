namespace Nimpression.Infrastructure.Realtime;

/// <summary>
/// 实时通信配置选项（包含 Outbox 轮询间隔等）。
/// </summary>
public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";

    /// <summary>
    /// Outbox 轮询间隔（毫秒）。默认 200ms。
    /// </summary>
    public int PollingIntervalMs { get; set; } = 200;

    public TimeSpan PollingInterval => TimeSpan.FromMilliseconds(PollingIntervalMs > 0 ? PollingIntervalMs : 200);
}
