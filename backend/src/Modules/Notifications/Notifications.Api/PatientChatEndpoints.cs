using System.Security.Claims;
using System.Text.Json.Nodes;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Notifications.Application.Chat;

namespace Notifications.Api;

/// <summary>
/// API chat của web bệnh nhân (contract chatbot §3): chỉ chuyển tiếp sang chatbot-service, không lưu cuộc trò chuyện.
/// Quyền Booking trên hồ sơ kiểm ở mỗi lượt vì có thể bị thu hồi giữa chừng.
/// </summary>
internal static class PatientChatEndpoints
{
    public const string RateLimitPolicy = "patient-chat";
    private const int TextMaxLength = 1000;

    public static void MapPatientChatRoutes(this RouteGroupBuilder notifications)
    {
        var chat = notifications.MapGroup("/patient/chat").RequireAuthorization("PatientOnly");
        chat.MapGet("/status", async ([FromServices] IChatbotGateway gateway, CancellationToken ct) =>
                TypedResults.Ok(new ChatStatus(await gateway.IsAvailableAsync(ct))))
            .WithSummary("Chatbot có dùng được không (Patient)").Produces<ChatStatus>();
        chat.MapPost("/conversations", OpenAsync)
            .WithSummary("Mở cuộc trò chuyện cho một hồ sơ (Patient)").Produces<ConversationOpened>(StatusCodes.Status201Created)
            .ProducesProblem(400).ProducesProblem(404);
        chat.MapPost("/conversations/{conversationId:guid}/messages", SendAsync)
            .RequireRateLimiting(RateLimitPolicy)
            .WithSummary("Gửi một lượt chat (Patient)").Produces<JsonObject>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(404).ProducesProblem(429).ProducesProblem(503);
        chat.MapDelete("/conversations/{conversationId:guid}", CloseAsync)
            .WithSummary("Đóng cuộc trò chuyện (Patient)").Produces(StatusCodes.Status204NoContent).ProducesProblem(404);
    }

    private static async Task<IResult> OpenAsync(ClaimsPrincipal user, OpenBody? body, [FromServices] IIdentityPatientsModule identity,
        [FromServices] IChatbotGateway gateway, CancellationToken ct)
    {
        if (UserId(user) is not { } userId)
            return TypedResults.Unauthorized();
        if (body is null || body.PatientId == Guid.Empty)
            return Problem(400, "InvalidInput");
        if (!await identity.CanAccessPatientAsync(userId, body.PatientId, AccessLevel.Booking, ct))
            return Problem(404, "ProfileNotFound");
        var conversationId = Guid.CreateVersion7();
        return TypedResults.Created($"/api/notifications/patient/chat/conversations/{conversationId:D}",
            new ConversationOpened(conversationId, body.PatientId, await gateway.IsAvailableAsync(ct)));
    }

    private static async Task<IResult> SendAsync(Guid conversationId, ClaimsPrincipal user, HttpRequest request, MessageBody? body,
        [FromServices] IIdentityPatientsModule identity, [FromServices] IChatbotGateway gateway, CancellationToken ct)
    {
        if (UserId(user) is not { } userId)
            return TypedResults.Unauthorized();
        var text = string.IsNullOrWhiteSpace(body?.Text) ? null : body.Text.Trim();
        var suggestion = string.IsNullOrWhiteSpace(body?.Suggestion) ? null : body.Suggestion.Trim();
        if (body is null || body.PatientId == Guid.Empty || (text is null) == (suggestion is null) || text is { Length: > TextMaxLength })
            return Problem(400, "InvalidInput");
        if (!await identity.CanAccessPatientAsync(userId, body.PatientId, AccessLevel.Booking, ct))
            return Problem(404, "ProfileNotFound");
        // Token đã được JwtBearer xác thực; chuyển nguyên để công cụ chatbot gọi API .NET thay người dùng.
        var token = request.Headers[HeaderNames.Authorization].ToString() is { } header
            && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
        if (string.IsNullOrEmpty(token))
            return TypedResults.Unauthorized();

        var result = await gateway.SendTurnAsync(new ChatTurnRequest(conversationId, userId, body.PatientId, Guid.CreateVersion7(),
            text, suggestion), token, ct);
        return result switch
        {
            { Failure: ChatTurnFailure.None, Reply: { } reply } => TypedResults.Ok(reply),
            { Failure: ChatTurnFailure.ConversationNotFound } => Problem(404, "ConversationNotFound"),
            { Failure: ChatTurnFailure.SessionExpired } => Problem(401, "SessionExpired"),
            { Failure: ChatTurnFailure.InvalidInput } => Problem(400, "InvalidInput"),
            _ => Problem(503, "ChatbotUnavailable"),
        };
    }

    private static async Task<IResult> CloseAsync(Guid conversationId, ClaimsPrincipal user, [FromServices] IChatbotGateway gateway,
        CancellationToken ct)
    {
        if (UserId(user) is not { } userId)
            return TypedResults.Unauthorized();
        return await gateway.DeleteConversationAsync(conversationId, userId, ct)
            ? TypedResults.NoContent() : Problem(404, "ConversationNotFound");
    }

    private static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id) && id != Guid.Empty ? id : null;

    private static IResult Problem(int status, string code) =>
        TypedResults.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed record OpenBody(Guid PatientId);
    private sealed record MessageBody(Guid PatientId, string? Text, string? Suggestion);
    private sealed record ChatStatus(bool Available);
    private sealed record ConversationOpened(Guid ConversationId, Guid PatientId, bool Available);
}
