namespace SmartHealthcare.Billing.Domain;

public sealed class RefundTransaction
{
    public Guid Id { get; set; }
    public Guid RefundRequestId { get; set; }
    public RefundRequest RefundRequest { get; set; } = null!;
    public Guid OriginalPaymentTransactionId { get; set; }
    public PaymentTransaction OriginalPaymentTransaction { get; set; } = null!;
    public string? ProviderRefundTransactionId { get; set; }
    public decimal RefundedAmount { get; set; }
    public Currency Currency { get; set; }
    public RefundTransactionStatus Status { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public string? ProviderResponsePayload { get; set; }
}
