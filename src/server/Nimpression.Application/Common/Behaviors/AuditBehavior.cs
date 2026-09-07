using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MediatR;
using Nimpression.Application.Common.Abstractions;
using Nimpression.Application.Common.Auditing;
using Nimpression.Application.Common.Results;

namespace Nimpression.Application.Common.Behaviors;

/// <summary>
/// 为实现 <see cref="IAuditableCommand"/> 的命令写审计（N1.1）。
///
/// 只在**成功**后写：失败的命令没有改变任何事实，为它留一条审计
/// 会让"谁改了什么"的查询混入大量未发生的变更。
/// 越权尝试的记录属于安全日志，走另一条路（N1.3），不占审计表。
/// </summary>
public sealed class AuditBehavior<TRequest, TResponse>(IAuditSink auditSink)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string[] SensitiveKeywords =
    [
        "password",
        "secret",
        "token",
        "authorization",
        "credential",
        "privatekey"
    ];

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken).ConfigureAwait(false);

        if (request is not IAuditableCommand auditable || IsFailure(response))
        {
            return response;
        }

        var entityId = ResolveEntityId(auditable, request, response);
        var afterJson = ResolveAfterJson(request);

        await auditSink.RecordAsync(
            auditable.AuditEntityType,
            entityId,
            auditable.AuditAction,
            beforeJson: null,
            afterJson: afterJson,
            cancellationToken).ConfigureAwait(false);

        return response;
    }

    private static Guid? ResolveEntityId(IAuditableCommand auditable, TRequest request, TResponse response)
    {
        if (auditable.AuditEntityId.HasValue && auditable.AuditEntityId.Value != Guid.Empty)
        {
            return auditable.AuditEntityId.Value;
        }

        var createdIdProp = request.GetType().GetProperty("CreatedId");
        if (createdIdProp != null)
        {
            var createdVal = createdIdProp.GetValue(request);
            if (createdVal is Guid cid && cid != Guid.Empty)
            {
                return cid;
            }
        }

        if (response is null)
        {
            return null;
        }

        if (response is Result<Guid> guidResult && guidResult.IsSuccess && guidResult.Value != Guid.Empty)
        {
            return guidResult.Value;
        }

        var type = response.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var isSuccessProp = type.GetProperty(nameof(Result.IsSuccess));
            if (isSuccessProp != null && (bool)isSuccessProp.GetValue(response)!)
            {
                var valueProp = type.GetProperty(nameof(Result<object>.Value));
                var val = valueProp?.GetValue(response);
                if (val is Guid g && g != Guid.Empty)
                {
                    return g;
                }
                if (val != null)
                {
                    var idProp = val.GetType().GetProperty("Id") ?? val.GetType().GetProperty("id");
                    if (idProp != null)
                    {
                        var idVal = idProp.GetValue(val);
                        if (idVal is Guid gid && gid != Guid.Empty)
                        {
                            return gid;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? ResolveAfterJson(TRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, SerializerOptions);
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
            {
                SanitizeJsonObject(obj);
                return obj.ToJsonString(SerializerOptions);
            }
            return json;
        }
        catch
        {
            return null;
        }
    }

    private static void SanitizeJsonObject(JsonObject obj)
    {
        var keysToRedact = new List<string>();
        foreach (var kvp in obj)
        {
            if (IsSensitiveKey(kvp.Key))
            {
                keysToRedact.Add(kvp.Key);
            }
            else if (kvp.Value is JsonObject childObj)
            {
                SanitizeJsonObject(childObj);
            }
            else if (kvp.Value is JsonArray arr)
            {
                SanitizeJsonArray(arr);
            }
        }

        foreach (var key in keysToRedact)
        {
            obj[key] = "[REDACTED]";
        }
    }

    private static void SanitizeJsonArray(JsonArray arr)
    {
        foreach (var item in arr)
        {
            if (item is JsonObject childObj)
            {
                SanitizeJsonObject(childObj);
            }
            else if (item is JsonArray childArr)
            {
                SanitizeJsonArray(childArr);
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        foreach (var keyword in SensitiveKeywords)
        {
            if (key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsFailure(TResponse response) => response switch
    {
        Result r => !r.IsSuccess,
        null => false,
        _ => response.GetType() is { IsGenericType: true } t
             && t.GetGenericTypeDefinition() == typeof(Result<>)
             && !(bool)t.GetProperty(nameof(Result.IsSuccess))!.GetValue(response)!,
    };
}
