using System.Text.Json;
using Nimpression.Application.Features.Realtime.Common;
using Nimpression.Application.Features.Realtime.DTOs;
using Nimpression.Domain.Entities.Standalone;
using Nimpression.Domain.Enums;

namespace Nimpression.Infrastructure.Realtime.Services;

/// <summary>
/// 领域事件发件箱（Outbox）到实时失效信号（Realtime Invalidation Signal）的映射器。
/// <para>
/// <b>核心设计灵魂：推送只作「失效信号」，绝不作数据通道。</b><br/>
/// 本映射器将完整的领域事件实体裁剪为仅包含 <c>{ kind, entityId, occurredAt }</c> 的纯失效信号，<br/>
/// 彻底去除所有业务载荷（标题、地址、金额、工单详情等）。
/// </para>
/// <para>
/// <b>设计理由：</b><br/>
/// 1. 推送通道不可信也不可靠——消息可能丢失、乱序、重放、被篡改。<br/>
///    若业务数据只从推送来，通道一出问题业务数据就损坏或产生状态漂移。<br/>
/// 2. 只推失效信号时，篡改推送内容不影响业务正确性——<br/>
///    最坏结果是客户端多拉一次或少拉一次，而少拉由重连后的增量补齐兜住。<br/>
/// 3. 这直接消解了原项目“WebSocket 崩了业务就乱”的系统性根因。
/// </para>
/// </summary>
public sealed class OutboxToRealtimeMapper : IOutboxToRealtimeMapper
{
    public OutboxRealtimeMapping Map(OutboxMessage outboxMessage)
    {
        ArgumentNullException.ThrowIfNull(outboxMessage);
        return Map(outboxMessage.Type, outboxMessage.PayloadJson, outboxMessage.OccurredAt);
    }

    public OutboxRealtimeMapping Map(string eventType, string payloadJson, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
        var root = doc.RootElement;

        var typeNormalized = eventType.Trim();
        if (typeNormalized.EndsWith("Event", StringComparison.OrdinalIgnoreCase))
        {
            typeNormalized = typeNormalized[..^5];
        }

        switch (typeNormalized)
        {
            case "JobTaskAssigned":
                {
                    var taskId = TryGetGuid(root, "JobTaskId", "jobTaskId", "Id", "id");
                    var driverId = TryGetGuid(root, "DriverId", "driverId");
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()),
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };
                    if (driverId.HasValue && driverId.Value != Guid.Empty)
                    {
                        groups.Add(RealtimeGroupNames.Driver(driverId.Value));
                    }

                    var msg = new RealtimeMessage(RealtimeEventKinds.TaskAssigned, taskId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, driverId);
                }

            case "JobTaskAcknowledged":
                {
                    var taskId = TryGetGuid(root, "JobTaskId", "jobTaskId", "Id", "id");
                    var driverId = TryGetGuid(root, "DriverId", "driverId");
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()),
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };
                    if (driverId.HasValue && driverId.Value != Guid.Empty)
                    {
                        groups.Add(RealtimeGroupNames.Driver(driverId.Value));
                    }

                    var msg = new RealtimeMessage(RealtimeEventKinds.TaskAcknowledged, taskId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, driverId);
                }

            case "JobTaskCompleted":
                {
                    var taskId = TryGetGuid(root, "JobTaskId", "jobTaskId", "Id", "id");
                    var driverId = TryGetGuid(root, "DriverId", "driverId");
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()),
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };
                    if (driverId.HasValue && driverId.Value != Guid.Empty)
                    {
                        groups.Add(RealtimeGroupNames.Driver(driverId.Value));
                    }

                    var msg = new RealtimeMessage(RealtimeEventKinds.TaskCompleted, taskId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, driverId);
                }

            case "DriverDeactivated":
                {
                    var driverId = TryGetGuid(root, "DriverId", "driverId", "Id", "id");
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()),
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };
                    if (driverId.HasValue && driverId.Value != Guid.Empty)
                    {
                        groups.Add(RealtimeGroupNames.Driver(driverId.Value));
                    }

                    var msg = new RealtimeMessage(RealtimeEventKinds.DriverDeactivated, driverId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, driverId);
                }

            case "IncidentReported":
                {
                    var incidentId = TryGetGuid(root, "IncidentId", "incidentId", "Id", "id");
                    var driverId = TryGetGuid(root, "DriverId", "driverId");
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()),
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };
                    if (driverId.HasValue && driverId.Value != Guid.Empty)
                    {
                        groups.Add(RealtimeGroupNames.Driver(driverId.Value));
                    }

                    var msg = new RealtimeMessage(RealtimeEventKinds.IncidentReported, incidentId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, driverId);
                }

            case "FineAccepted":
                {
                    var fineId = TryGetGuid(root, "FineId", "fineId", "Id", "id");
                    var driverId = TryGetGuid(root, "DriverId", "driverId");
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()),
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };
                    if (driverId.HasValue && driverId.Value != Guid.Empty)
                    {
                        groups.Add(RealtimeGroupNames.Driver(driverId.Value));
                    }

                    var msg = new RealtimeMessage(RealtimeEventKinds.FineAccepted, fineId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, driverId);
                }

            case "PayslipFinalised":
                {
                    var payslipId = TryGetGuid(root, "PayslipId", "payslipId", "Id", "id");
                    var driverId = TryGetGuid(root, "DriverId", "driverId");
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };
                    if (driverId.HasValue && driverId.Value != Guid.Empty)
                    {
                        groups.Add(RealtimeGroupNames.Driver(driverId.Value));
                    }

                    var msg = new RealtimeMessage(RealtimeEventKinds.PayslipFinalised, payslipId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, driverId);
                }

            case "NewsPublished":
                {
                    var newsPostId = TryGetGuid(root, "NewsPostId", "newsPostId", "Id", "id");
                    var audienceVal = TryGetInt(root, "Audience", "audience") ?? (int)NewsAudience.All;
                    var audience = (NewsAudience)audienceVal;

                    var groups = new List<string>();
                    switch (audience)
                    {
                        case NewsAudience.Drivers:
                            groups.Add(RealtimeGroupNames.Role(UserRole.Driver.ToString()));
                            break;
                        case NewsAudience.Dispatchers:
                            groups.Add(RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()));
                            groups.Add(RealtimeGroupNames.Role(UserRole.Admin.ToString()));
                            break;
                        case NewsAudience.All:
                        default:
                            groups.Add(RealtimeGroupNames.All);
                            break;
                    }

                    var msg = new RealtimeMessage(RealtimeEventKinds.NewsPublished, newsPostId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, null);
                }

            case "ServiceThresholdReached":
                {
                    var vehicleId = TryGetGuid(root, "VehicleId", "vehicleId", "Id", "id");
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Dispatcher.ToString()),
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };

                    var msg = new RealtimeMessage(RealtimeEventKinds.VehicleServiceThresholdReached, vehicleId ?? Guid.Empty, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, null);
                }

            default:
                {
                    var entityId = TryGetGuid(root, "Id", "id", "EntityId", "entityId", "JobTaskId", "DriverId", "VehicleId") ?? Guid.Empty;
                    var groups = new List<string>
                {
                    RealtimeGroupNames.Role(UserRole.Admin.ToString())
                };
                    var kind = ConvertPascalToKebabOrSnake(typeNormalized);
                    var msg = new RealtimeMessage(kind, entityId, occurredAt);
                    return new OutboxRealtimeMapping(msg, groups, null);
                }
        }
    }

    private static Guid? TryGetGuid(JsonElement root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (root.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String && Guid.TryParse(prop.GetString(), out var guid))
                {
                    return guid;
                }
            }
        }
        return null;
    }

    private static int? TryGetInt(JsonElement root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (root.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var val))
                {
                    return val;
                }
                if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsedInt))
                {
                    return parsedInt;
                }
            }
        }
        return null;
    }

    private static string ConvertPascalToKebabOrSnake(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return "domain.event";
        }

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < pascalCase.Length; i++)
        {
            var c = pascalCase[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('.');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
