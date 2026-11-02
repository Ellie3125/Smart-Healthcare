using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class RefundTransaction
{
    public Guid Id { get; set; }
    public Guid RefundRequestId { get; set; }
    public Guid OriginalPaymentTransactionId { get; set; }
    public string? ProviderRefundTransactionId { get; set; }
    public decimal RefundedAmount { get; set; }
    public string Currency { get; set; } = null!;
    public RefundTransactionStatus Status { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public string ProviderResponsePayload { get; set; } = null!;
}
