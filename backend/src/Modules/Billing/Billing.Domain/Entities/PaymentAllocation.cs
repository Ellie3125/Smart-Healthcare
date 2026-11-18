namespace Billing.Domain.Entities;

public sealed class PaymentAllocation
{
    public Guid Id { get; set; }
    public Guid PaymentTransactionId { get; set; }
    public Guid BillingItemId { get; set; }
    public decimal AllocatedAmount { get; set; }
    public DateTime AllocatedAtUtc { get; set; }
}
