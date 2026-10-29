namespace SmartHealthcare.Scheduling.Domain;

public sealed class QueueTicket
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public Guid TargetRoomId { get; set; }
    public Guid? TargetStationId { get; set; }
    public ServiceStation? TargetStation { get; set; }
    public Guid? ServiceOrderId { get; set; }
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
