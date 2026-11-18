using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdentityPatients.Contracts;
using IdentityPatients.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Notifications.Api;
using Notifications.Application.Chat;
using Notifications.Infrastructure;
using Xunit;

namespace ArchitectureTests;

// API chat .NET → chatbot-service (contract chatbot §2, §3; Q-C1, Q-C2, Q-C4 chốt 18/11/2026).
public sealed class PatientChatProxyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gateway_without_configuration_reports_unavailable_without_calling_out()
    {
        var stub = new StubHandler();
        await using var provider = GatewayServices(stub, baseUrl: null);
        var gateway = provider.GetRequiredService<IChatbotGateway>();

        Assert.False(await gateway.IsAvailableAsync(Ct));
        Assert.Equal(ChatTurnFailure.Unavailable, (await gateway.SendTurnAsync(Turn(), "tok", Ct)).Failure);
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task Gateway_sends_key_token_and_identity_and_strips_intent()
    {
        var stub = new StubHandler { Respond = _ => Json(HttpStatusCode.OK, """{"reply":"Chào bạn","intent":"GREETING","suggestions":[],"proposal":null,"options":[]}""") };
        await using var provider = GatewayServices(stub);
        var turn = Turn();

        var result = await provider.GetRequiredService<IChatbotGateway>().SendTurnAsync(turn, "user-token", Ct);

        Assert.Equal(ChatTurnFailure.None, result.Failure);
        Assert.Equal("Chào bạn", result.Reply!["reply"]!.GetValue<string>());
        Assert.False(result.Reply.ContainsKey("intent"));
        var sent = Assert.Single(stub.Requests);
        Assert.Equal("http://chatbot:8000/v1/turns", sent.Uri);
        Assert.Equal("internal-key-0123456789", sent.ChatbotKey);
        Assert.Equal("Bearer user-token", sent.Authorization);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal(turn.ConversationId, body.RootElement.GetProperty("conversationId").GetGuid());
        Assert.Equal(turn.PatientId, body.RootElement.GetProperty("patientId").GetGuid());
        Assert.Equal("vi", body.RootElement.GetProperty("locale").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "ConversationNotFound", ChatTurnFailure.ConversationNotFound)]
    [InlineData(HttpStatusCode.Unauthorized, "SessionExpired", ChatTurnFailure.SessionExpired)]
    [InlineData(HttpStatusCode.Unauthorized, "InvalidChatbotKey", ChatTurnFailure.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "ChatbotUnavailable", ChatTurnFailure.Unavailable)]
    [InlineData(HttpStatusCode.UnprocessableEntity, "ValidationError", ChatTurnFailure.InvalidInput)]
    [InlineData(HttpStatusCode.InternalServerError, null, ChatTurnFailure.Unavailable)]
    public async Task Gateway_maps_chatbot_errors(HttpStatusCode status, string? code, ChatTurnFailure expected)
    {
        var stub = new StubHandler { Respond = _ => Json(status, code is null ? "oops" : $$"""{"code":"{{code}}"}""") };
        await using var provider = GatewayServices(stub);

        Assert.Equal(expected, (await provider.GetRequiredService<IChatbotGateway>().SendTurnAsync(Turn(), "t", Ct)).Failure);
    }

    [Fact]
    public async Task Gateway_treats_connection_failure_as_unavailable_and_caches_health()
    {
        var stub = new StubHandler { Respond = _ => throw new HttpRequestException("refused") };
        await using var provider = GatewayServices(stub);
        var gateway = provider.GetRequiredService<IChatbotGateway>();

        Assert.Equal(ChatTurnFailure.Unavailable, (await gateway.SendTurnAsync(Turn(), "t", Ct)).Failure);
        Assert.False(await gateway.IsAvailableAsync(Ct));
        Assert.False(await gateway.IsAvailableAsync(Ct));
        Assert.Single(stub.Requests, r => r.Uri.EndsWith("/health", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Endpoints_require_patient_channel_and_booking_access_each_turn()
    {
        var patientId = Guid.NewGuid();
        var identity = DispatchProxy.Create<IIdentityPatientsModule, AccessIdentity>();
        ((AccessIdentity)(object)identity).Allowed = patientId;
        var gateway = new FakeGateway();
        await using var app = await StartAsync(identity, gateway);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        const string status = "/api/notifications/patient/chat/status";

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(status, Ct)).StatusCode);
        Authorize(client, Roles.Receptionist, Channels.Staff);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(status, Ct)).StatusCode);
        var userId = Authorize(client, Roles.Patient, Channels.Patient);
        Assert.True((await client.GetFromJsonAsync<JsonObject>(status, Ct))!["available"]!.GetValue<bool>());

        using var denied = await client.PostAsJsonAsync("/api/notifications/patient/chat/conversations", new { patientId = Guid.NewGuid() }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var opened = await client.PostAsJsonAsync("/api/notifications/patient/chat/conversations", new { patientId }, Ct);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var conversationId = (await opened.Content.ReadFromJsonAsync<JsonObject>(Ct))!["conversationId"]!.GetValue<Guid>();
        var messages = $"/api/notifications/patient/chat/conversations/{conversationId:D}/messages";

        using var both = await client.PostAsJsonAsync(messages, new { patientId, text = "a", suggestion = "VIEW_APPOINTMENTS" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
        using var tooLong = await client.PostAsJsonAsync(messages, new { patientId, text = new string('a', 1001) }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        using var other = await client.PostAsJsonAsync(messages, new { patientId = Guid.NewGuid(), text = "xin chào" }, Ct);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);

        using var ok = await client.PostAsJsonAsync(messages, new { patientId, text = "  xin chào  " }, Ct);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("ok", (await ok.Content.ReadFromJsonAsync<JsonObject>(Ct))!["reply"]!.GetValue<string>());
        var sent = Assert.Single(gateway.Turns);
        Assert.Equal((conversationId, userId, patientId, "xin chào", (string?)null),
            (sent.Request.ConversationId, sent.Request.UserId, sent.Request.PatientId, sent.Request.Text, sent.Request.Suggestion));
        Assert.Equal(client.DefaultRequestHeaders.Authorization!.Parameter, sent.Token);

        gateway.Next = ChatTurnFailure.Unavailable;
        using var down = await client.PostAsJsonAsync(messages, new { patientId, suggestion = "VIEW_APPOINTMENTS" }, Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, down.StatusCode);
        Assert.Equal("ChatbotUnavailable", (await down.Content.ReadFromJsonAsync<JsonObject>(Ct))!["code"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/notifications/patient/chat/conversations/{conversationId:D}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/notifications/patient/chat/conversations/{Guid.NewGuid():D}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Messages_are_limited_to_ten_per_minute_per_account()
    {
        var patientId = Guid.NewGuid();
        var identity = DispatchProxy.Create<IIdentityPatientsModule, AccessIdentity>();
        ((AccessIdentity)(object)identity).Allowed = patientId;
        await using var app = await StartAsync(identity, new FakeGateway());
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        Authorize(client, Roles.Patient, Channels.Patient);
        var messages = $"/api/notifications/patient/chat/conversations/{Guid.NewGuid():D}/messages";

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(messages, new { patientId, text = "hi" }, Ct)).StatusCode);
        using var limited = await client.PostAsJsonAsync(messages, new { patientId, text = "hi" }, Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("TooManyRequests", (await limited.Content.ReadFromJsonAsync<JsonObject>(Ct))!["code"]!.GetValue<string>());
        // Tài khoản khác có hạn mức riêng.
        Authorize(client, Roles.Patient, Channels.Patient);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(messages, new { patientId, text = "hi" }, Ct)).StatusCode);
    }

    private static ChatTurnRequest Turn() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "xin chào", null);

    private static ServiceProvider GatewayServices(StubHandler stub, string? baseUrl = "http://chatbot:8000")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Chatbot:BaseUrl"] = baseUrl,
            ["Chatbot:InternalKey"] = "internal-key-0123456789",
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(config);
        services.AddNotificationsInfrastructure("Server=unused;Database=none;");
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = stub));
        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static async Task<WebApplication> StartAsync(IIdentityPatientsModule identity, IChatbotGateway gateway)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["Jwt:SigningKey"] = new string('k', 32);
        builder.Configuration["Jwt:Issuer"] = builder.Configuration["Jwt:Audience"] = "PatientChatProxyTests";
        builder.Services.AddIdentityPatientsInfrastructure(builder.Configuration, "Server=unused;Database=none;");
        builder.Services.AddNotificationsApi();
        builder.Services.AddSingleton(identity).AddSingleton(gateway);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapNotificationsEndpoints();
        await app.StartAsync(Ct);
        return app;
    }

    private static Guid Authorize(HttpClient client, string role, string channel)
    {
        var userId = Guid.NewGuid();
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(
            new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("PatientChatProxyTests", "PatientChatProxyTests",
                [new("role", role), new("sub", userId.ToString()), new(AppClaims.Channel, channel)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 32))),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return userId;
    }

    private sealed record SentRequest(string Uri, string? ChatbotKey, string? Authorization, string? Body);

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; init; } = _ => new(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new(request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Chatbot-Key", out var key) ? key.Single() : null,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return Respond(request);
        }
    }

    private sealed class FakeGateway : IChatbotGateway
    {
        public List<(ChatTurnRequest Request, string Token)> Turns { get; } = [];
        public ChatTurnFailure Next { get; set; }
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<ChatTurnResult> SendTurnAsync(ChatTurnRequest request, string bearerToken, CancellationToken ct)
        {
            Turns.Add((request, bearerToken));
            return Task.FromResult(Next == ChatTurnFailure.None
                ? new ChatTurnResult(ChatTurnFailure.None, new JsonObject { ["reply"] = "ok" }) : new ChatTurnResult(Next));
        }

        public Task<bool> DeleteConversationAsync(Guid conversationId, Guid userId, CancellationToken ct) =>
            Task.FromResult(Turns.Any(t => t.Request.ConversationId == conversationId && t.Request.UserId == userId));
    }

    public class AccessIdentity : DispatchProxy
    {
        public Guid Allowed { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == nameof(IIdentityPatientsModule.CanAccessPatientAsync)
            ? Task.FromResult((Guid)args![1]! == Allowed)
            : throw new NotSupportedException(method?.Name);
    }
}
