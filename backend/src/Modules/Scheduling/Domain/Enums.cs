namespace SmartHealthcare.Scheduling.Domain;


public enum AppointmentStatus
{
    Scheduled,
    CheckedIn,
    Completed,
    Cancelled,
    Absent,
    Rescheduled
}

public enum BookingChannel
{
    Web,
    App,
    Chatbot,
    Counter
}

public enum ClaimSource
{
    App,
    Web,
    Chatbot,
    Counter
}

public enum QueueStatus
{
    Waiting,
    Called,
    Deferred,
    InProgress,
    Finished,
    Cancelled
}

public enum ReservationStatus
{
    Held,
    Confirmed,
    Expired,
    Cancelled
}

public enum SessionStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public enum TicketType
{
    InitialConsultation,
    CLS,
    ReturnConsultation
}
