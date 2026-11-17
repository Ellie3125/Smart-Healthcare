using Shared.Abstractions;

namespace Scheduling.Contracts;

public sealed record ClsTicketCancelled(Guid EventId, Guid QueueTicketId, Guid EncounterId, Guid PatientId, Guid ServiceOrderItemId,
    string Reason, DateTime OccurredAtUtc, Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;
    public int Version => CurrentVersion;
}
