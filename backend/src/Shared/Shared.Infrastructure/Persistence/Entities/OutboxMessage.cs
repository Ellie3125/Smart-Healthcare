using Shared.Infrastructure.Persistence.Enums;

namespace Shared.Infrastructure.Persistence.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    // ID chéo module: workflow.Operation, không FK
    public Guid? OperationId { get; set; }
    public EventType EventType { get; set; }
    public AggregateType AggregateType { get; set; }
    public Guid AggregateId { get; set; }
    public string PayloadJson { get; set; } = null!;
    public int RetryCount { get; set; }
    public OutboxMessageStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
