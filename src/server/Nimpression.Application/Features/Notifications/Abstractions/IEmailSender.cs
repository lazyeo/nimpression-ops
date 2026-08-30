namespace Nimpression.Application.Features.Notifications.Abstractions;

/// <summary>
/// 邮件投递底层抽象接口。
/// 严禁静默降级（_COMMON.md 强制要求）：投递失败必须抛出异常，由上层记录状态并按退避策略重试。
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// 发送电子邮件。
    /// </summary>
    /// <param name="to">收件人邮箱</param>
    /// <param name="subject">邮件主题</param>
    /// <param name="body">邮件正文（支持 HTML 或纯文本）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}
