using Shared.Abstractions;

namespace Scheduling.Contracts;

public sealed record QueueCalled(Guid EventId, Guid QueueTicketId, Guid EncounterId, Guid PatientId, Guid RoomId, Guid StationId,
    string TicketNumber, int SequenceNumber, string TicketType, Guid PerformedByUserId, DateTime OccurredAtUtc, Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;
    public int Version => CurrentVersion;
}
