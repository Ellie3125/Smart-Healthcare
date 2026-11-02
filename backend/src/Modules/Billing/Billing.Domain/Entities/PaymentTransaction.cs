using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid PaymentIntentId { get; set; }
    public string TransactionReference { get; set; } = null!;
    public string? ProviderTransactionId { get; set; }
    public decimal TransactionAmount { get; set; }
    public string Currency { get; set; } = null!;
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public string? ProviderRawData { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
