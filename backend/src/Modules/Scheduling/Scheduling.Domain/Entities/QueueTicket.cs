using Scheduling.Domain.Enums;

namespace Scheduling.Domain.Entities;

public sealed class QueueTicket
{
    public Guid Id { get; set; }
    // ID chéo module: clinical.Encounter, không FK
    public Guid EncounterId { get; set; }
    // ID chéo module: identity.Room, không FK
    public Guid TargetRoomId { get; set; }
    public Guid? TargetStationId { get; set; }
    // ID chéo module: clinical.ServiceOrder, không FK
    public Guid? ServiceOrderId { get; set; }
    // ID chéo module: clinical.ServiceOrderItem, không FK
    public Guid? ServiceOrderItemId { get; set; }
    public string TicketNumber { get; set; } = null!;
    public TicketType TicketType { get; set; }
    public int SequenceNumber { get; set; }
    public QueueStatus QueueStatus { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? CalledAtUtc { get; set; }
    public DateTime? ServedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
