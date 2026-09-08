using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Nimpression.Application.Common.Results;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Infrastructure.Notifications;
using Nimpression.Infrastructure.Notifications.Compliance;

namespace Nimpression.Application.Tests.Notifications;

public class ComplianceExpirySchedulerTests
{
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IServiceScope _scope = Substitute.For<IServiceScope>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IComplianceExpiryScanner _scanner = Substitute.For<IComplianceExpiryScanner>();
    private readonly ILogger<ComplianceExpiryScanSchedulerBackgroundService> _logger = Substitute.For<ILogger<ComplianceExpiryScanSchedulerBackgroundService>>();

    public ComplianceExpirySchedulerTests()
    {
        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_serviceProvider);
        _serviceProvider.GetService(typeof(IComplianceExpiryScanner)).Returns(_scanner);
    }

    [Fact]
    public async Task ScanAsync_WhenInvoked_ResolvesScannerAndReturnsSentCount()
    {
        // Arrange
        _scanner.ScanAndNotifyAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(3));

        var options = Options.Create(new NotificationOptions
        {
            ComplianceScanIntervalMs = 60000,
            RunComplianceScanOnStartup = false
        });

        var service = new ComplianceExpiryScanSchedulerBackgroundService(_scopeFactory, _logger, options);

        // Act
        var result = await service.ScanAsync(CancellationToken.None);

        // Assert
        result.Should().Be(3);
        await _scanner.Received(1).ScanAndNotifyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_WhenScannerReturnsFailure_ReturnsZeroWithoutThrowing()
    {
        // Arrange
        _scanner.ScanAndNotifyAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Failure(Error.Validation("scan_failed", "Database connection lost")));

        var options = Options.Create(new NotificationOptions
        {
            ComplianceScanIntervalMs = 60000,
            RunComplianceScanOnStartup = false
        });

        var service = new ComplianceExpiryScanSchedulerBackgroundService(_scopeFactory, _logger, options);

        // Act
        var result = await service.ScanAsync(CancellationToken.None);

        // Assert
        result.Should().Be(0);
        await _scanner.Received(1).ScanAndNotifyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartedWithImmediateCancellation_StopsGracefully()
    {
        // Arrange
        var options = Options.Create(new NotificationOptions
        {
            ComplianceScanIntervalMs = 100,
            InitialStartupDelayMs = 0,
            RunComplianceScanOnStartup = true
        });

        _scanner.ScanAndNotifyAsync(Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(1));

        var service = new ComplianceExpiryScanSchedulerBackgroundService(_scopeFactory, _logger, options);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        await service.StartAsync(cts.Token);
        await service.StopAsync(CancellationToken.None);

        // Assert
        // Service starts and stops without hanging or unhandled exception
    }
}
