namespace SmartHealthcare.Scheduling.Domain;

public sealed class ActiveStationAllocation
{
    public Guid StationId { get; set; }
    public ServiceStation Station { get; set; } = null!;
    public Guid CurrentPatientId { get; set; }
    public Guid CurrentTicketId { get; set; }
    public QueueTicket CurrentTicket { get; set; } = null!;
    public DateTime AllocatedAtUtc { get; set; }
}
