using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nimpression.Application.Features.Notifications.Abstractions;
using Nimpression.Infrastructure.Notifications.Compliance;
using Nimpression.Infrastructure.Notifications.Outbox;
using Nimpression.Infrastructure.Notifications.Persistence;
using Nimpression.Infrastructure.Notifications.Smtp;

namespace Nimpression.Infrastructure.Notifications;

/// <summary>
/// 通知与邮件基础设施层服务注册扩展方法。
/// </summary>
public static class NotificationInfrastructureExtensions
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));

        // 仓储注册
        services.AddScoped<IPartnerContactRepository, PartnerContactRepository>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        services.AddScoped<IEmailLogRepository, EmailLogRepository>();

        // 邮件发送器与 Outbox 消费
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<INotificationOutboxService, NotificationOutboxService>();
        services.AddScoped<IComplianceExpiryScanner, ComplianceExpiryScanner>();

        // 后台消费 Worker
        services.AddHostedService<NotificationOutboxProcessorBackgroundService>();

        return services;
    }
}
