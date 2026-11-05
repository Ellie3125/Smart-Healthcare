namespace Shared.Abstractions;

/// <summary>
/// Sự kiện tích hợp giữa các module, ghi vào Outbox cùng transaction nghiệp vụ (GP v2.8 §8.2, MSG-03).
/// EventId trùng OutboxMessage.Id; handler ghi EventId vào ProcessedEvent để không xử lý lặp.
/// Payload tối thiểu, không chứa bệnh án.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    /// <summary>Phiên bản schema của sự kiện; tăng khi đổi payload theo kiểu không tương thích.</summary>
    int Version { get; }

    DateTime OccurredAtUtc { get; }

    /// <summary>workflow.Operation gây ra sự kiện, dùng làm correlation; null nếu không có.</summary>
    Guid? OperationId { get; }
}
