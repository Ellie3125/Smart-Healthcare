namespace Scheduling.Contracts;

public sealed record ClinicalQueueTicket(Guid QueueTicketId, Guid EncounterId, string TicketNumber,
    string QueueStatus, DateTime QueuedAtUtc);
public sealed record ClinicalQueuePage(bool HasMore, IReadOnlyList<ClinicalQueueTicket> Items);
