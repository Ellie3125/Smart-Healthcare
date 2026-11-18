using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Chat;

namespace Notifications.Infrastructure.Chat;

public sealed class ChatbotOptions
{
    public const string Section = "Chatbot";

    /// <summary>Địa chỉ nội bộ, ví dụ http://chatbot:8000. Trống thì chatbot coi như tạm ngưng.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Khớp CHATBOT_INTERNAL_KEY của chatbot-service; chỉ đặt qua biến môi trường hoặc user-secrets.</summary>
    public string? InternalKey { get; set; }
}

/// <summary>Kết quả /health dùng chung cho mọi request trong 5 giây (contract §3.1).</summary>
public sealed class ChatbotHealthCache
{
    private (DateTime CheckedAtUtc, bool Available)? _last;
    private readonly Lock _gate = new();

    public bool TryGet(DateTime nowUtc, out bool available)
    {
        lock (_gate)
        {
            available = _last?.Available ?? false;
            return _last is { } last && nowUtc - last.CheckedAtUtc < TimeSpan.FromSeconds(5);
        }
    }

    public void Set(DateTime nowUtc, bool available)
    {
        lock (_gate)
            _last = (nowUtc, available);
    }
}

internal sealed class HttpChatbotGateway(HttpClient http, IOptions<ChatbotOptions> options, ChatbotHealthCache cache,
    TimeProvider time, ILogger<HttpChatbotGateway> logger) : IChatbotGateway
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        if (cache.TryGet(now, out var cached))
            return cached;
        var available = false;
        if (BaseUri() is { } baseUri)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                var health = await http.GetFromJsonAsync<JsonObject>(new Uri(baseUri, "health"), Json, timeout.Token);
                available = health?["available"]?.GetValue<bool>() == true;
            }
            catch (Exception e) when (!ct.IsCancellationRequested
                && e is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
            {
                logger.LogWarning("Không gọi được /health của chatbot: {Error}", e.GetType().Name);
            }
        }
        cache.Set(now, available);
        return available;
    }

    public async Task<ChatTurnResult> SendTurnAsync(ChatTurnRequest request, string bearerToken, CancellationToken ct)
    {
        if (BaseUri() is not { } baseUri)
            return new(ChatTurnFailure.Unavailable);
        using var message = Request(HttpMethod.Post, new Uri(baseUri, "v1/turns"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        message.Content = JsonContent.Create(new
        {
            request.ConversationId, request.UserId, request.PatientId, request.MessageId, request.Text, request.Suggestion,
            Locale = "vi",
        }, options: Json);
        try
        {
            using var response = await http.SendAsync(message, ct);
            if (response.StatusCode == HttpStatusCode.OK
                && await response.Content.ReadFromJsonAsync<JsonObject>(Json, ct) is { } reply)
            {
                reply.Remove("intent");
                return new(ChatTurnFailure.None, reply);
            }
            return new(await FailureAsync(response, ct));
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is HttpRequestException or JsonException or OperationCanceledException)
        {
            logger.LogWarning("Lượt chat không tới được chatbot: {Error}", e.GetType().Name);
            return new(ChatTurnFailure.Unavailable);
        }
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        if (BaseUri() is not { } baseUri)
            return true; // Chatbot tắt: phía .NET không giữ gì để xóa.
        using var message = Request(HttpMethod.Delete, new Uri(baseUri, $"v1/conversations/{conversationId:D}"));
        message.Content = JsonContent.Create(new { UserId = userId }, options: Json);
        using var response = await http.SendAsync(message, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    private HttpRequestMessage Request(HttpMethod method, Uri uri)
    {
        var message = new HttpRequestMessage(method, uri);
        message.Headers.Add("X-Chatbot-Key", options.Value.InternalKey);
        return message;
    }

    private Uri? BaseUri() => !string.IsNullOrWhiteSpace(options.Value.InternalKey)
        && Uri.TryCreate(options.Value.BaseUrl?.TrimEnd('/') + "/", UriKind.Absolute, out var uri) ? uri : null;

    private async Task<ChatTurnFailure> FailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var code = await CodeAsync(response, ct);
        switch (response.StatusCode, code)
        {
            case (HttpStatusCode.NotFound, "ConversationNotFound"):
                return ChatTurnFailure.ConversationNotFound;
            case (HttpStatusCode.Unauthorized, "SessionExpired"):
                return ChatTurnFailure.SessionExpired;
            case (HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest, _):
                return ChatTurnFailure.InvalidInput;
            case (HttpStatusCode.ServiceUnavailable, _):
                return ChatTurnFailure.Unavailable;
            default:
                // Sai X-Chatbot-Key hoặc lỗi lạ phía chatbot là lỗi cấu hình; người dùng chỉ thấy tạm ngưng.
                logger.LogError("Chatbot trả {Status} {Code}", (int)response.StatusCode, code);
                return ChatTurnFailure.Unavailable;
        }
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<JsonObject>(Json, ct))?["code"]?.GetValue<string>();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
