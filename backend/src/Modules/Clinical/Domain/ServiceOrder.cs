namespace SmartHealthcare.Clinical.Domain;

public sealed class ServiceOrder
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public Encounter Encounter { get; set; } = null!;
    public int OrderRoundNumber { get; set; }
    public DateTime OrderedAtUtc { get; set; }
    public Guid PrescribedByDoctorId { get; set; }
    public OrderBatchStatus OrderBatchStatus { get; set; }
    public bool HasActiveReturnTicket { get; set; }
}
