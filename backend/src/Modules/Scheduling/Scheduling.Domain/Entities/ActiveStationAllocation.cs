namespace Scheduling.Domain.Entities;

public sealed class ActiveStationAllocation
{
    public Guid StationId { get; set; }
    // ID chéo module: identity.Patient, không FK
    public Guid CurrentPatientId { get; set; }
    public Guid CurrentTicketId { get; set; }
    public DateTime AllocatedAtUtc { get; set; }
}
