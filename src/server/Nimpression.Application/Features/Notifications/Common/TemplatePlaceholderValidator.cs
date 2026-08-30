using System.Text.RegularExpressions;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Features.Notifications.Common;

/// <summary>
/// 邮件模板占位符校验器（F11.2）。
/// 确保保存或更新模板时，所有业务必须的占位符均被完整定义，否则返回 422 Unprocessable。
/// </summary>
public static partial class TemplatePlaceholderValidator
{
    private static readonly Dictionary<string, IReadOnlyList<string>> RequiredPlaceholdersMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [NotificationTemplateKeys.ServiceDueReminder] = ["VehicleRego", "CurrentOdometer"],
            [NotificationTemplateKeys.ComplianceExpiryWarning] = ["ExpiryType", "VehicleRego", "ExpiryDate"],
            [NotificationTemplateKeys.IncidentNotification] = ["Severity", "VehicleRego"],
            [NotificationTemplateKeys.FineAcceptedNotice] = ["FineRef"]
        };

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// 获取指定模板键所需的占位符集合（不带双花括号）。
    /// </summary>
    public static IReadOnlyList<string> GetRequiredPlaceholders(string templateKey)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return Array.Empty<string>();
        }

        return RequiredPlaceholdersMap.TryGetValue(templateKey.Trim(), out var placeholders)
            ? placeholders
            : Array.Empty<string>();
    }

    /// <summary>
    /// 从文本中提取所有包含的占位符变量名。
    /// </summary>
    public static HashSet<string> ExtractPlaceholders(string text)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        var matches = PlaceholderRegex().Matches(text);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                result.Add(match.Groups[1].Value.Trim());
            }
        }

        return result;
    }

    /// <summary>
    /// 校验模板内容中是否包含全部必须的占位符。
    /// </summary>
    public static Result ValidateRequiredPlaceholders(
        string templateKey,
        string subjectEn,
        string subjectZh,
        string bodyEn,
        string bodyZh)
    {
        var required = GetRequiredPlaceholders(templateKey);
        if (required.Count == 0)
        {
            return Result.Success();
        }

        var combinedEn = $"{subjectEn} {bodyEn}";
        var combinedZh = $"{subjectZh} {bodyZh}";

        var extractedEn = ExtractPlaceholders(combinedEn);
        var extractedZh = ExtractPlaceholders(combinedZh);

        var missingEn = required.Where(p => !extractedEn.Contains(p)).ToList();
        var missingZh = required.Where(p => !extractedZh.Contains(p)).ToList();

        if (missingEn.Count > 0 || missingZh.Count > 0)
        {
            var missingDetails = new List<string>();
            if (missingEn.Count > 0)
            {
                missingDetails.Add($"English version is missing: {string.Join(", ", missingEn.Select(p => $"{{{{{p}}}}}"))}");
            }
            if (missingZh.Count > 0)
            {
                missingDetails.Add($"Chinese version is missing: {string.Join(", ", missingZh.Select(p => $"{{{{{p}}}}}"))}");
            }

            return Error.Unprocessable(
                "missing_template_placeholders",
                $"Template '{templateKey.ToUpperInvariant()}' requires placeholders. {string.Join("; ", missingDetails)}");
        }

        return Result.Success();
    }
}
