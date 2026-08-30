using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Nimpression.Integration.Tests.Notifications.Fixtures;

/// <summary>
/// Mailpit 本地测试客户端（SMTP: 1025, REST API: 8025）。
/// 用于在集成测试中清空邮件箱、检索投递邮件与断言内容。
/// </summary>
public sealed class MailpitTestClient : IDisposable
{
    private readonly HttpClient _httpClient;
    public const string MailpitApiUrl = "http://localhost:8025";

    public MailpitTestClient(string baseAddress = MailpitApiUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task ClearAllMessagesAsync()
    {
        try
        {
            await _httpClient.DeleteAsync("/api/v1/messages");
        }
        catch
        {
            // 忽略连接异常（若 Mailpit 未就绪）
        }
    }

    public async Task<List<MailpitMessageSummary>> GetAllMessagesAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<MailpitMessagesResponse>("/api/v1/messages");
            return response?.Messages ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<int> GetMessageCountAsync()
    {
        var messages = await GetAllMessagesAsync();
        return messages.Count;
    }

    public async Task<MailpitMessageSummary?> FindMessageBySubjectAsync(string subjectPart)
    {
        var messages = await GetAllMessagesAsync();
        return messages.FirstOrDefault(m => m.Subject.Contains(subjectPart, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public sealed class MailpitMessagesResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("messages")]
    public List<MailpitMessageSummary> Messages { get; set; } = [];
}

public sealed class MailpitMessageSummary
{
    [JsonPropertyName("ID")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("To")]
    public List<MailpitAddress> To { get; set; } = [];

    [JsonPropertyName("From")]
    public MailpitAddress? From { get; set; }
}

public sealed class MailpitAddress
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Address")]
    public string Address { get; set; } = string.Empty;
}
