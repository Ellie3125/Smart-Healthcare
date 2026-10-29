namespace SmartHealthcare.Billing.Domain;

public sealed class PaymentAllocation
{
    public Guid Id { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public PaymentTransaction PaymentTransaction { get; set; } = null!;
    public Guid BillingItemId { get; set; }
    public BillingItem BillingItem { get; set; } = null!;
    public decimal AllocatedAmount { get; set; }
    public DateTime AllocatedAtUtc { get; set; }
}
