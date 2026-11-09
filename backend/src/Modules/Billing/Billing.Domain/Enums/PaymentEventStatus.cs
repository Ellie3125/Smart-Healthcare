namespace Billing.Domain.Enums;

public enum PaymentEventStatus
{
    Received,
    Processed,
    DuplicateIgnored,
    Error
}
