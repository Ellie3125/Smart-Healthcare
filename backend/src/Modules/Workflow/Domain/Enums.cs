namespace SmartHealthcare.Workflow.Domain;


public enum ActionType
{
    INSERT,
    UPDATE,
    DELETE,
    CANCEL,
    REFUND,
    PUBLISH
}

public enum AggregateType
{
    Reservation,
    Appointment,
    Payment,
    CLSResult
}

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

public enum CurrentStep
{
    ProfileSelection,
    DoctorSelection,
    SlotHeld,
    PaymentInitiated,
    Finalized,
    Abandoned
}

public enum EventType
{
    SlotHeldEvent,
    BookingConfirmedEvent,
    PaymentSettledEvent,
    CLSResultPublishedEvent
}

public enum OperationStatus
{
    Pending,
    Running,
    WaitingExternal,
    Succeeded,
    Failed,
    NeedsAction,
    ManualReview
}

public enum OperationType
{
    HoldSlot,
    ConfirmBooking,
    RescheduleBooking,
    CancelBooking,
    AllocateStation,
    ApproveResult,
    ProcessRefund
}

public enum OutboxMessageStatus
{
    Pending,
    Processing,
    Published,
    DeadLetter
}

public enum TargetEntityType
{
    BookingAttempt,
    Reservation,
    Appointment,
    BillingItem,
    QueueTicket,
    CLSResult
}
