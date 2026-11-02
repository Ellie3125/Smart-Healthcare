using Clinical.Domain.Enums;

namespace Clinical.Domain.Entities;

public sealed class EncounterDiagnosis
{
    public Guid Id { get; set; }
    public Guid EncounterId { get; set; }
    public DiagnosisType DiagnosisType { get; set; }
    public string ICD10Code { get; set; } = null!;
    public string DiagnosisName { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public string ClinicalNotes { get; set; } = null!;
    public DateTime DiagnosedAtUtc { get; set; }
}
