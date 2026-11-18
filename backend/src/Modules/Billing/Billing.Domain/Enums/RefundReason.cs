namespace Billing.Domain.Enums;

public enum RefundReason
{
    CancelledBefore0500,
    PaidUnallocated,
    DuplicatePayment,
    HospitalCancelled,
    SystemError
}
