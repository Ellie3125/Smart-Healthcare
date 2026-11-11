using Billing.Domain.Enums;

namespace Billing.Domain.Entities;

public sealed class RefundRequest
{
    public Guid Id { get; set; }
    public Guid BillingItemId { get; set; }
    public Guid OriginalPaymentTransactionId { get; set; }
    // ID chéo module: scheduling.Appointment, không FK
    public Guid? AppointmentId { get; set; }
    public decimal RequestedAmount { get; set; }
    public RefundReason RefundReason { get; set; }
    public PolicyRuleApplied PolicyRuleApplied { get; set; }
    public RefundRequestStatus Status { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    // ID chéo module: identity.Staff, không FK
    public Guid? DecisionByStaffId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}
