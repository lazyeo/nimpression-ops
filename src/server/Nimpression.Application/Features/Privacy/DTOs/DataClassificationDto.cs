namespace Nimpression.Application.Features.Privacy.DTOs;

/// <summary>
/// 数据分级清单项（AC N2.2 数据分级）。
/// 标注每个数据项的敏感度等级、法律依据、法定/业务保留期、静止加密状态及收集目的。
/// </summary>
public sealed record DataClassificationDto(
    string EntityName,
    string FieldName,
    string SensitivityLevel,
    string LegalBasis,
    string RetentionPeriod,
    bool IsEncryptedAtRest,
    string EncryptionMechanism,
    string PurposeDescription);

/// <summary>
/// 系统权威数据资产分级分类目录。
/// 符合 NZ Privacy Act 2020 (IPP 1, 4, 9, 12), Employment Relations Act 2000, Holidays Act 2003 与 Tax Administration Act 1994。
/// </summary>
public static class DataClassificationCatalog
{
    private static readonly IReadOnlyList<DataClassificationDto> Items =
    [
        // Driver 实体
        new("Driver", "PhoneEnc", "Restricted PII", "Privacy Act 2020 IPP 1-4; Operational contact", "Employment duration + 7 years", true, "AES-256-GCM (enc:v1:)", "Driver operational dispatch and emergency communication"),
        new("Driver", "AddressEnc", "Restricted PII", "Privacy Act 2020 IPP 1-4; Employment records", "Employment duration + 7 years", true, "AES-256-GCM (enc:v1:)", "Employment contract execution & statutory payroll address"),
        new("Driver", "EmergencyContactEnc", "Restricted PII", "Privacy Act 2020 IPP 1-4; Health & Safety at Work Act 2015", "Employment duration", true, "AES-256-GCM (enc:v1:)", "Workplace safety incident response"),
        new("Driver", "LicenceClass / LicenceExpiry", "Internal Compliance", "Land Transport Act 1998 s31; Health & Safety at Work Act 2015", "Employment duration", false, "TLS 1.3 in transit", "Driver licensing compliance and automated expiry alerts"),
        new("Driver", "HourlyRate / PerTripRate / PerKmRate", "Confidential Commercial", "Employment Relations Act 2000 s130", "7 years (Tax Administration Act 1994 s22)", false, "Role-based access control + TLS 1.3", "Three-tier hybrid payroll calculation"),
        new("Driver", "EmployeeNo", "Internal Identifier", "Privacy Act 2020 IPP 1", "7 years post-termination", false, "Indexed internal key", "Unique employment record association"),

        // Vehicle 实体
        new("Vehicle", "VinEnc", "Confidential Asset PII", "Land Transport Act 1998; NZTA WOF/COF regulation", "Vehicle lifecycle + 7 years", true, "AES-256-GCM (enc:v1:)", "Statutory vehicle identification and insurance claim verification"),
        new("Vehicle", "Rego", "Internal Operational", "Land Transport Act 1998", "Vehicle lifecycle + 7 years", false, "Unique index + TLS 1.3", "Fleet dispatch tracking and toll/fine attribution"),
        new("Vehicle", "OdometerKm", "Internal Operational", "Land Transport Act 1998; Maintenance policy", "7 years", false, "TLS 1.3 in transit", "WOF/COF maintenance scheduling and trip distance reconciliation"),

        // Timesheets / ShiftEntry 实体
        new("ShiftEntry", "ClockInLat / ClockInLng / ClockOutLat / ClockOutLng", "Restricted Geolocation", "Privacy Act 2020 IPP 9 (Data Retention Limit)", "90 days (automatic purge policy)", false, "Purged after 90 days (IPP 9 compliance)", "Shift clock-in/out physical presence audit at job sites"),
        new("ShiftEntry", "ClockInAt / ClockOutAt / BreakMinutes", "Confidential Payroll", "Holidays Act 2003 s81; Employment Relations Act 2000 s130", "7 years", false, "Append-only audit trail + TLS 1.3", "Wage entitlement and statutory work hour verification"),

        // Payroll / Payslips 实体
        new("Payslip", "GrossPay / NetPay / PayeTax / KiwiSaver", "Highly Restricted Financial", "Tax Administration Act 1994 s22; KiwiSaver Act 2006", "7 years (IRD statutory retention)", false, "Column-level isolation + TLS 1.3", "Inland Revenue (IRD) compliance and employee wage payment"),

        // Compliance / IncidentReport 实体
        new("IncidentReport", "ThirdPartyInfoEnc", "Restricted Third-Party PII", "Privacy Act 2020 IPP 11(e); Insurance Law Reform Act 1977", "7 years (Statutory claim limitation)", true, "AES-256-GCM (enc:v1:)", "Accident liability resolution and commercial insurer disclosure"),
        new("IncidentReport", "Location / Description / Severity", "Confidential Operational", "Health & Safety at Work Act 2015 s56", "7 years", false, "TLS 1.3 in transit", "Workplace safety investigation and WorkSafe NZ reporting"),
        new("IncidentReport", "PhotoKeys", "Confidential Evidence", "Evidence Act 2006; Insurance Law", "7 years", false, "MinIO / S3 SSE-KMS", "Damage inspection evidence and insurer audit"),

        // Compliance / Fine 实体
        new("Fine", "NoticeNumber / Amount / Status", "Internal Financial", "Land Transport (Offences and Penalties) Regulations", "7 years", false, "TLS 1.3 in transit", "Traffic infringement management and driver dispute resolution"),

        // Identity / User 实体
        new("User", "Email", "Restricted PII", "Privacy Act 2020 IPP 1-3", "Employment duration + 7 years", false, "Unique index + TLS 1.3", "Authentication, notifications, and official communications"),
        new("User", "PasswordHash", "Highly Restricted Auth Credential", "Privacy Act 2020 IPP 5 (Security Safeguards)", "Active account duration", true, "One-way cryptographic hash (Argon2id/PBKDF2)", "User authentication and session security"),
        new("RefreshToken", "TokenHash / Expiry", "Confidential Session Credential", "Privacy Act 2020 IPP 5", "30 days (purged on expiry/revocation)", false, "Cryptographic hash + Auto-purge", "JWT session extension"),

        // Standalone / AuditEvent 实体
        new("AuditEvent", "ActorUserId / Action / EntityId / BeforeJson / AfterJson", "Confidential System Audit", "Privacy Act 2020 IPP 5; Companies Act 1993", "7 years immutable retention", false, "DB Trigger Append-Only Protection", "Non-repudiation and security forensics"),

        // Standalone / DataSubjectRequest 实体
        new("DataSubjectRequest", "SubjectUserId / Kind / Status / CompletedAt", "Confidential Compliance Record", "Privacy Act 2020 IPP 6 & IPP 7", "7 years", false, "TLS 1.3 in transit", "Data Subject Access Request (DSAR) audit trail")
    ];

    public static IReadOnlyList<DataClassificationDto> GetAll() => Items;
}
