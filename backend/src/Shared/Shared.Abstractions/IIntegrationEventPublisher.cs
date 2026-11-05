namespace Shared.Abstractions;

/// <summary>
/// Phát integration event cho module khác (Giải pháp v2.11 §8.2, MSG-02).
/// Bản cài đặt ghi sự kiện vào shared.OutboxMessage trong CÙNG transaction nghiệp vụ đang mở:
/// nghiệp vụ rollback thì sự kiện cũng mất. Không gửi ngay cho handler; worker phát Outbox sau commit.
/// Phải gọi bên trong <see cref="IUnitOfWork"/>
/// hoặc transaction module đã mở; gọi ngoài transaction là lỗi.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <param name="e">Sự kiện; payload tối thiểu, không chứa bệnh án (MSG-03).</param>
    /// <param name="aggregateId">Id của bản ghi gốc sinh ra sự kiện (ví dụ PaymentIntent, Appointment).</param>
    Task PublishAsync<TEvent>(TEvent e, Guid aggregateId, CancellationToken ct) where TEvent : IIntegrationEvent;
}
