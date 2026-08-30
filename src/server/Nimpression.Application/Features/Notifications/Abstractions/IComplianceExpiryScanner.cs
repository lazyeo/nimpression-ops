using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Notifications.Abstractions;

/// <summary>
/// 车辆合规到期预警定时扫描服务接口（F3.5 / F11）。
/// 扫描 WOF / COF / 保险在 30 / 14 / 7 天内到期的车辆并触发邮件提醒。
/// “今天”必须基于 IDateTimeProvider 注入，绝不依赖真实时钟。
/// </summary>
public interface IComplianceExpiryScanner
{
    /// <summary>
    /// 执行合规到期扫描并生成预警邮件记录。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>本次扫描触发的新邮件发送数量</returns>
    Task<Result<int>> ScanAndNotifyAsync(CancellationToken cancellationToken = default);
}
