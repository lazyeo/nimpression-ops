namespace Nimpression.Infrastructure.Notifications;

/// <summary>
/// 通知与邮件配置选项（包含 Outbox 轮询间隔等）。
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Outbox 轮询间隔（毫秒）。默认 500ms。
    /// </summary>
    public int PollingIntervalMs { get; set; } = 500;

    public TimeSpan PollingInterval => TimeSpan.FromMilliseconds(PollingIntervalMs > 0 ? PollingIntervalMs : 500);
}
