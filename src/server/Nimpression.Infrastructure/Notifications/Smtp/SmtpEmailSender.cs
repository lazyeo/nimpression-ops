using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Infrastructure.Notifications.Smtp;

/// <summary>
/// 基于标准 SMTP 的邮件投递实现。
/// <para>
/// <b>严禁静默降级（_COMMON.md / CLAUDE.md 2.3）：</b><br/>
/// 邮件是"发出去就没了"的操作，若发送异常被 catch 后吞掉，业务方将无法事后察觉。<br/>
/// 因此此处必须让异常正常向上抛出，由 Outbox 消费层捕获、记录至 EmailLog 并进入退避重试队列。
/// </para>
/// </summary>
public sealed partial class SmtpEmailSender(
    IOptions<EmailSettings> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _settings = options.Value;

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var host = string.IsNullOrWhiteSpace(_settings.Host) ? "localhost" : _settings.Host;
        var port = _settings.Port <= 0 ? 1025 : _settings.Port;
        var from = string.IsNullOrWhiteSpace(_settings.FromAddress) ? "notifications@nimpression.co.nz" : _settings.FromAddress;
        var fromName = string.IsNullOrWhiteSpace(_settings.FromDisplayName) ? "Nimpression Fleet Operations" : _settings.FromDisplayName;

        LogAttemptingDelivery(logger, to, host, port, subject);

        using var client = new SmtpClient(host, port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            EnableSsl = _settings.EnableSsl,
            Timeout = _settings.TimeoutMs
        };

        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(to.Trim());

        // 发送失败时异常向外传播，禁止吞异常
        await client.SendMailAsync(message, cancellationToken);

        LogDeliverySucceeded(logger, to, subject);
    }

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Attempting SMTP delivery to {To} via {Host}:{Port} with subject '{Subject}'")]
    private static partial void LogAttemptingDelivery(ILogger logger, string to, string host, int port, string subject);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "SMTP delivery succeeded for {To} (Subject: {Subject})")]
    private static partial void LogDeliverySucceeded(ILogger logger, string to, string subject);
}
