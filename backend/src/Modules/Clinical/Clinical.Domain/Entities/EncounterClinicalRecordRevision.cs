using Clinical.Domain.Enums;

namespace Clinical.Domain.Entities;

// Chỉ ghi thêm: mỗi lần phát hành hoặc đính chính bệnh án ghi một dòng chứa nội dung SAU thao tác đó.
// Bản hiện hành vẫn là EncounterClinicalRecord; dòng có RevisionNumber lớn nhất trùng nội dung với nó.
public sealed class EncounterClinicalRecordRevision
{
    public const int ReasonMaxLength = 1000;

    public Guid Id { get; set; }
    public Guid ClinicalRecordId { get; set; }
    public int RevisionNumber { get; set; }
    public ClinicalRecordRevisionKind Kind { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? Symptoms { get; set; }
    public string? MedicalHistory { get; set; }
    public string? ClinicalExaminationNotes { get; set; }
    public string? InitialClinicalImpression { get; set; }
    public string? PostCLSConclusion { get; set; }
    public string? TreatmentPlan { get; set; }
    public string? FollowUpInstructions { get; set; }
    // Bản chụp JSON các EncounterDiagnosis tại thời điểm này (mảng; có thể rỗng), chỉ để đọc lại lịch sử.
    public string DiagnosesJson { get; set; } = "[]";
    // Bắt buộc khi Kind = Amended.
    public string? Reason { get; set; }
    // ID chéo module: identity.Doctor, không FK
    public Guid ChangedByDoctorId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
