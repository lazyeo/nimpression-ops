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

    /// <summary>
    /// 合规到期扫描轮询间隔（毫秒）。默认 1 小时 (3,600,000 ms)。
    /// 预警窗口为 30/14/7 天粒度，每小时定时轮询搭配启动即扫可确保及时发送并快速容灾。
    /// </summary>
    public int ComplianceScanIntervalMs { get; set; } = 3600 * 1000;

    public TimeSpan ComplianceScanInterval => TimeSpan.FromMilliseconds(ComplianceScanIntervalMs > 0 ? ComplianceScanIntervalMs : 3600 * 1000);

    /// <summary>
    /// 启动时是否在初始延迟后立即执行一次合规扫描。默认 true。
    /// </summary>
    public bool RunComplianceScanOnStartup { get; set; } = true;

    /// <summary>
    /// 启动时首次扫描的延迟等待时间（毫秒）。默认 5000ms (5s)。
    /// </summary>
    public int InitialStartupDelayMs { get; set; } = 5000;

    public TimeSpan InitialStartupDelay => TimeSpan.FromMilliseconds(InitialStartupDelayMs >= 0 ? InitialStartupDelayMs : 5000);
}
