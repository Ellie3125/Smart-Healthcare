namespace SmartHealthcare.Billing.Domain;

public sealed class BillingItem
{
    public Guid Id { get; set; }
    public Guid? EncounterId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? ServiceOrderItemId { get; set; }
    public Guid PriceListItemId { get; set; }
    public ItemType ItemType { get; set; }
    public string ItemNameSnapshot { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public BillingItemStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
