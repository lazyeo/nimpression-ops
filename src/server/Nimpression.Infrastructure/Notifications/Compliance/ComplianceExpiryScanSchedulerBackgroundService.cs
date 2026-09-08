using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nimpression.Application.Features.Notifications.Abstractions;

namespace Nimpression.Infrastructure.Notifications.Compliance;

/// <summary>
/// 车辆合规到期预警扫描后台定时调度服务（F3.5 / F11 / W43）。
/// <para>
/// <b>调度与时钟机制：</b><br/>
/// 1. <b>触发频率与依据</b>：合规到期（WOF/COF/保险）为自然日粒度（30/14/7天），后台服务默认每 1 小时轮询一次（搭配服务启动自检），确保在营业时间内及时发出且在服务重启后自动补发。<br/>
/// 2. <b>幂等与去重复用</b>：直接复用 <see cref="IComplianceExpiryScanner.ScanAndNotifyAsync"/> 内置的 <c>CORR-{TYPE}-{REGO}-{DAYS}DAY</c> 幂等键与数据库唯一索引约束（SqlState 23505），同一天/同一窗口内重复扫描绝对不会多次发信。<br/>
/// 3. <b>新西兰时区与夏令时（DST）</b>：通过底层 <c>IDateTimeProvider.NzToday</c> 基于 <c>Pacific/Auckland</c> 时区及 <c>DateOnly</c> 自然日计算，完全不受 UTC 跨天或夏令时 23/25 小时昼长变化干扰。<br/>
/// 4. <b>测试驱动友好</b>：对外暴露 <see cref="ScanAsync"/> 方法，允许集成测试与单元测试无竞态按需触发。
/// </para>
/// </summary>
public sealed partial class ComplianceExpiryScanSchedulerBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ComplianceExpiryScanSchedulerBackgroundService> logger,
    IOptions<NotificationOptions>? options = null) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = options?.Value.ComplianceScanInterval ?? TimeSpan.FromHours(1);
    private readonly bool _runOnStartup = options?.Value.RunComplianceScanOnStartup ?? true;
    private readonly TimeSpan _initialDelay = options?.Value.InitialStartupDelay ?? TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(logger, (int)_pollingInterval.TotalMinutes);

        if (_runOnStartup)
        {
            try
            {
                if (_initialDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_initialDelay, stoppingToken);
                }

                await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                LogServiceStopped(logger);
                return;
            }
            catch (Exception ex)
            {
                LogScanLoopError(logger, ex);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
                await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogScanLoopError(logger, ex);
            }
        }

        LogServiceStopped(logger);
    }

    /// <summary>
    /// 手动或测试驱动触发单次合规扫描。
    /// </summary>
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IComplianceExpiryScanner>();
        var result = await scanner.ScanAndNotifyAsync(cancellationToken);

        if (result.IsSuccess)
        {
            LogScanCompleted(logger, result.Value);
            return result.Value;
        }

        LogScanFailed(logger, result.Error?.Message ?? "Unknown error");
        return 0;
    }

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "ComplianceExpiryScanSchedulerBackgroundService started with interval {IntervalMinutes}m.")]
    private static partial void LogServiceStarted(ILogger logger, int intervalMinutes);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Information,
        Message = "ComplianceExpiryScanSchedulerBackgroundService stopped.")]
    private static partial void LogServiceStopped(ILogger logger);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Information,
        Message = "ComplianceExpiryScan completed successfully. Sent {SentCount} notifications.")]
    private static partial void LogScanCompleted(ILogger logger, int sentCount);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Warning,
        Message = "ComplianceExpiryScan finished with failure: {ErrorMessage}")]
    private static partial void LogScanFailed(ILogger logger, string errorMessage);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Error,
        Message = "Unexpected error in ComplianceExpiryScanScheduler background loop.")]
    private static partial void LogScanLoopError(ILogger logger, Exception exception);
}
