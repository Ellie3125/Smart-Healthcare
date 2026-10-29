namespace SmartHealthcare.Workflow.Domain;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid? OperationId { get; set; }
    public Operation? Operation { get; set; }
    public EventType EventType { get; set; }
    public AggregateType AggregateType { get; set; }
    public Guid AggregateId { get; set; }
    public string PayloadJson { get; set; } = null!;
    public int RetryCount { get; set; }
    public OutboxMessageStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
