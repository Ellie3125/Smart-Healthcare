namespace Shared.Abstractions;

/// <summary>
/// Xử lý một loại integration event ở module nhận (Giải pháp v2.11 §8.2, MSG-02, MSG-04).
/// Outbox có thể giao lặp (MSG-05): handler phải đánh dấu bằng <see cref="IProcessedEventStore.TryMarkAsync"/>
/// cùng transaction với tác dụng nghiệp vụ, và bỏ qua khi đã xử lý.
/// Một sự kiện có thể có nhiều handler; mỗi handler chống lặp riêng theo tên của nó.
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent e, CancellationToken ct);
}
