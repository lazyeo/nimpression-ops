namespace Nimpression.Application.Features.Notifications.Common;

/// <summary>
/// 预置邮件模板键常量。
/// </summary>
public static class NotificationTemplateKeys
{
    /// <summary>
    /// 车辆保养里程阈值提醒（发给维保伙伴 Maintenance）。
    /// 占位符：{{VehicleRego}}, {{CurrentOdometer}}
    /// </summary>
    public const string ServiceDueReminder = "SERVICE_DUE_REMINDER";

    /// <summary>
    /// 车辆合规到期预警（发给年检伙伴 Inspection）。
    /// 占位符：{{ExpiryType}}, {{VehicleRego}}, {{ExpiryDate}}
    /// </summary>
    public const string ComplianceExpiryWarning = "COMPLIANCE_EXPIRY_WARNING";

    /// <summary>
    /// 车队事故通报（发给保险伙伴 Insurer）。
    /// 占位符：{{Severity}}, {{VehicleRego}}
    /// </summary>
    public const string IncidentNotification = "INCIDENT_NOTIFICATION";

    /// <summary>
    /// 交通罚单责任确认通知（发给保险伙伴 Insurer）。
    /// 占位符：{{FineRef}}
    /// </summary>
    public const string FineAcceptedNotice = "FINE_ACCEPTED_NOTICE";
}
