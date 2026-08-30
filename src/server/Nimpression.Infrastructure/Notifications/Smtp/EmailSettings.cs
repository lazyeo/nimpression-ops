namespace Nimpression.Infrastructure.Notifications.Smtp;

/// <summary>
/// 邮件 SMTP 发送配置项。
/// 本地开发与测试默认对接 Mailpit（localhost:1025）。
/// </summary>
public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromAddress { get; set; } = "notifications@nimpression.co.nz";
    public string FromDisplayName { get; set; } = "Nimpression Fleet Operations";
    public bool EnableSsl { get; set; }
    public int TimeoutMs { get; set; } = 10000;
}
