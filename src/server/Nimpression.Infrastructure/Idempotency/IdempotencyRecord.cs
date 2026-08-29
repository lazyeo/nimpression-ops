namespace Nimpression.Infrastructure.Idempotency;

/// <summary>
/// 离线幂等重放记录实体（F5.4）。
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>
    /// 幂等键（客户端生成的 ClientRequestId）。
    /// </summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>
    /// 请求载荷 SHA-256 哈希值。
    /// </summary>
    public string RequestHash { get; private set; } = string.Empty;

    /// <summary>
    /// 首次成功执行的响应内容 JSON。
    /// </summary>
    public string ResponseJson { get; private set; } = string.Empty;

    /// <summary>
    /// 首次执行的 HTTP 响应状态码。
    /// </summary>
    public int StatusCode { get; private set; }

    /// <summary>
    /// 记录创建时间（UTC）。
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    private IdempotencyRecord() { }

    public IdempotencyRecord(string key, string requestHash, string responseJson, int statusCode, DateTimeOffset createdAt)
    {
        Key = key;
        RequestHash = requestHash;
        ResponseJson = responseJson;
        StatusCode = statusCode;
        CreatedAt = createdAt;
    }
}
