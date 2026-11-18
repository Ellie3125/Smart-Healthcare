using Shared.Abstractions;

namespace Scheduling.Contracts;

public sealed record ReturnTicketActivated(Guid EventId, Guid EncounterId, Guid PatientId, Guid ServiceOrderId, Guid QueueTicketId,
    Guid RoomId, Guid DoctorId, string TicketNumber, DateTime OccurredAtUtc, Guid? OperationId) : IIntegrationEvent
{
    public const int CurrentVersion = 1;
    public int Version => CurrentVersion;
}
