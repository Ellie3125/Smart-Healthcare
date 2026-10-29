namespace SmartHealthcare.Billing.Domain;

public sealed class RefundRequest
{
    public Guid Id { get; set; }
    public Guid BillingItemId { get; set; }
    public BillingItem BillingItem { get; set; } = null!;
    public Guid OriginalPaymentTransactionId { get; set; }
    public PaymentTransaction OriginalPaymentTransaction { get; set; } = null!;
    public Guid? AppointmentId { get; set; }
    public decimal RequestedAmount { get; set; }
    public RefundReason RefundReason { get; set; }
    public PolicyRuleApplied PolicyRuleApplied { get; set; }
    public RefundRequestStatus Status { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public Guid? DecisionByStaffId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}
