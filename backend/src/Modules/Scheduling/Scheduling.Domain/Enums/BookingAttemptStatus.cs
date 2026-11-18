namespace Scheduling.Domain.Enums;

public enum BookingAttemptStatus
{
    Draft,
    Held,
    WaitingPayment,
    Confirming,
    Confirmed,
    NeedsReselection,
    PaidUnallocated,
    Cancelled
}
