namespace SmartHealthcare.Clinical.Domain;

public sealed class EncounterDiagnosis
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public Encounter Encounter { get; set; } = null!;
    public DiagnosisType DiagnosisType { get; set; }
    public string? ICD10Code { get; set; }
    public string DiagnosisName { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public string? ClinicalNotes { get; set; }
    public DateTime DiagnosedAtUtc { get; set; }
}
