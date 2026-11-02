namespace Scheduling.Domain.Entities;

public sealed class ActivePatientEngagement
{
    // ID chéo module: identity.Patient, không FK
    public Guid PatientId { get; set; }
    // ID chéo module: clinical.Encounter, không FK
    public Guid EncounterId { get; set; }
    public Guid CurrentStationId { get; set; }
    public Guid CurrentTicketId { get; set; }
    public DateTime AcquiredAtUtc { get; set; }
}
