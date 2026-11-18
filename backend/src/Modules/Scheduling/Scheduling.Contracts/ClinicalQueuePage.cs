namespace Scheduling.Contracts;

public sealed record ClinicalQueueTicket(Guid QueueTicketId, Guid EncounterId, string TicketNumber,
    string QueueStatus, DateTime QueuedAtUtc);
public sealed record ClinicalQueuePage(bool HasMore, IReadOnlyList<ClinicalQueueTicket> Items);

public sealed record JourneyQueueTicket(Guid QueueTicketId, Guid RoomId, Guid? ServiceOrderItemId, string TicketNumber,
    string TicketType, string QueueStatus, DateTime QueuedAtUtc, DateTime? CalledAtUtc, DateTime? ServedAtUtc, DateTime? CompletedAtUtc);
public sealed record PatientJourneySnapshot(Guid? AppointmentId, Guid? RoomId, string? AppointmentStatus, IReadOnlyList<JourneyQueueTicket> Tickets);
