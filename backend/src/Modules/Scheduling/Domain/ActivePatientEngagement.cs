namespace SmartHealthcare.Scheduling.Domain;

public sealed class ActivePatientEngagement
{
    public Guid PatientId { get; set; }
    public Guid EncounterId { get; set; }
    public Guid CurrentStationId { get; set; }
    public ServiceStation CurrentStation { get; set; } = null!;
    public Guid CurrentTicketId { get; set; }
    public QueueTicket CurrentTicket { get; set; } = null!;
    public DateTime AcquiredAtUtc { get; set; }
}
