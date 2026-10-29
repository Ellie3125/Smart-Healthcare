namespace SmartHealthcare.Billing.Domain;

public sealed class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid PaymentIntentId { get; set; }
    public PaymentIntent PaymentIntent { get; set; } = null!;
    public string TransactionReference { get; set; } = null!;
    public string? ProviderTransactionId { get; set; }
    public decimal TransactionAmount { get; set; }
    public Currency Currency { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? ProviderRawData { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
