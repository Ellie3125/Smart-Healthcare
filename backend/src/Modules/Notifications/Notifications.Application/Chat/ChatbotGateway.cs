using System.Text.Json.Nodes;

namespace Notifications.Application.Chat;

/// <summary>Một lượt chat .NET gửi sang chatbot-service (contract chatbot §2.1). Danh tính do .NET điền, không lấy từ client.</summary>
public sealed record ChatTurnRequest(Guid ConversationId, Guid UserId, Guid PatientId, Guid MessageId, string? Text, string? Suggestion);

public enum ChatTurnFailure { None, Unavailable, ConversationNotFound, SessionExpired, InvalidInput }

/// <param name="Reply">Phản hồi của chatbot đã bỏ <c>intent</c> (contract §3.1).</param>
public sealed record ChatTurnResult(ChatTurnFailure Failure, JsonObject? Reply = null);

/// <summary>Chỉ chuyển tiếp tới chatbot-service trong mạng nội bộ (Q-C1, Q-C4 chốt 18/11/2026); .NET không lưu cuộc trò chuyện (Q-C2).</summary>
public interface IChatbotGateway
{
    Task<bool> IsAvailableAsync(CancellationToken ct);

    Task<ChatTurnResult> SendTurnAsync(ChatTurnRequest request, string bearerToken, CancellationToken ct);

    /// <returns>false khi chatbot không có cuộc trò chuyện của người dùng này.</returns>
    Task<bool> DeleteConversationAsync(Guid conversationId, Guid userId, CancellationToken ct);
}
