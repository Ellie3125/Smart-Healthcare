namespace SmartHealthcare.Clinical.Domain;

public sealed class EncounterClinicalRecord
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public Encounter Encounter { get; set; } = null!;
    public EncounterClinicalRecordStatus Status { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? Symptoms { get; set; }
    public string? MedicalHistory { get; set; }
    public string? ClinicalExaminationNotes { get; set; }
    public string? InitialClinicalImpression { get; set; }
    public string? PostCLSConclusion { get; set; }
    public string? TreatmentPlan { get; set; }
    public string? FollowUpInstructions { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public Guid UpdatedByDoctorId { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
