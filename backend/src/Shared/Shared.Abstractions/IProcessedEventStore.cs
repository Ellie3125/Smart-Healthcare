namespace Shared.Abstractions;

/// <summary>
/// Chống xử lý lặp integration event ở bên nhận (Giải pháp v2.11 §8.2, MSG-02, MSG-05).
/// Ghi cặp (EventId, HandlerName) vào shared.ProcessedEvent, unique theo cặp này, trong CÙNG transaction
/// với tác dụng nghiệp vụ của handler: tác dụng rollback thì dấu đã xử lý cũng mất.
/// </summary>
public interface IProcessedEventStore
{
    /// <returns><c>true</c> nếu lần này được đánh dấu và handler phải xử lý; <c>false</c> nếu cặp đã có, handler bỏ qua.</returns>
    Task<bool> TryMarkAsync(Guid eventId, string handlerName, CancellationToken ct);
}
