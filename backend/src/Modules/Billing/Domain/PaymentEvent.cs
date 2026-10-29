namespace SmartHealthcare.Billing.Domain;

public sealed class PaymentEvent
{
    public Guid Id { get; set; }
    public Guid? PaymentTransactionId { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
    public PaymentEventProvider Provider { get; set; }
    public string EventId { get; set; } = null!;
    public EventType EventType { get; set; }
    public string PayloadHash { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public DateTime ReceivedAtUtc { get; set; }
    public PaymentEventStatus Status { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
