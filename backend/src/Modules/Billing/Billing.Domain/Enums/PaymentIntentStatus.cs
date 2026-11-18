namespace Billing.Domain.Enums;

public enum PaymentIntentStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    Unknown,
    Expired,
    Cancelled
}
