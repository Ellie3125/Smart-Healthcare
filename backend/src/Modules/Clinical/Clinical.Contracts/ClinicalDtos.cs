namespace Clinical.Contracts;

public sealed record StartEncounterCommand(
    Guid AppointmentId,
    Guid PatientId,
    Guid OriginalDoctorId,
    Guid OriginalRoomId,
    string SpecialtySnapshot,
    string DoctorRankSnapshot);

public sealed record EncounterDto(
    Guid Id,
    Guid AppointmentId,
    Guid PatientId,
    Guid OriginalDoctorId,
    Guid OriginalRoomId,
    string SpecialtySnapshot,
    string DoctorRankSnapshot,
    DateTime CheckedInAtUtc,
    string Status,
    DateTime? CompletedAtUtc);

public sealed record EncounterSummaryDto(
    Guid Id,
    Guid AppointmentId,
    Guid PatientId,
    string SpecialtySnapshot,
    string DoctorRankSnapshot,
    DateTime CheckedInAtUtc,
    string Status,
    DateTime? CompletedAtUtc);

public sealed record VitalSignDto(
    Guid Id,
    Guid EncounterId,
    decimal? WeightKg,
    decimal? HeightCm,
    decimal? SystolicBP,
    decimal? DiastolicBP,
    int? PulseRate,
    decimal? BodyTemperature,
    decimal? SpO2,
    Guid RecordedByStaffId,
    DateTime RecordedAtUtc);

public sealed record EncounterClinicalRecordDto(
    Guid Id,
    Guid EncounterId,
    string Status,
    string? ChiefComplaint,
    string? Symptoms,
    string? MedicalHistory,
    string? ClinicalExaminationNotes,
    string? InitialClinicalImpression,
    string? PostCLSConclusion,
    string? TreatmentPlan,
    string? FollowUpInstructions,
    DateTime RecordedAtUtc,
    DateTime? PublishedAtUtc,
    DateTime LastUpdatedUtc,
    Guid UpdatedByDoctorId);

public sealed record EncounterDiagnosisDto(
    Guid Id,
    Guid EncounterId,
    string DiagnosisType,
    string ICD10Code,
    string DiagnosisName,
    bool IsPrimary,
    string ClinicalNotes,
    DateTime DiagnosedAtUtc);

public sealed record ServiceOrderItemDto(
    Guid Id,
    Guid ServiceOrderId,
    Guid MedicalServiceId,
    string ServiceNameSnapshot,
    decimal AppliedPrice,
    string ItemStatus,
    bool IsMandatory,
    DateTime? CompletedAtUtc,
    CLSResultDto? Result);

public sealed record ServiceOrderDto(
    Guid Id,
    Guid EncounterId,
    int OrderRoundNumber,
    DateTime OrderedAtUtc,
    Guid PrescribedByDoctorId,
    string OrderBatchStatus,
    bool HasActiveReturnTicket,
    IReadOnlyList<ServiceOrderItemDto> Items);

public sealed record CLSResultDto(
    Guid Id,
    Guid ServiceOrderItemId,
    string ResultSummary,
    string DetailedFindings,
    string ResultStatus,
    Guid? PerformedByStaffId,
    Guid? ApprovedByStaffId,
    DateTime? PerformedAtUtc,
    DateTime? ApprovedAtUtc,
    DateTime? PublishedAtUtc);

public sealed record PrescriptionItemDto(
    Guid Id,
    Guid PrescriptionId,
    Guid MedicationId,
    string MedicationNameSnapshot,
    string ActiveIngredientSnapshot,
    string Dosage,
    string Route,
    string Frequency,
    int DurationDays,
    int Quantity,
    string UsageInstructions);

public sealed record PrescriptionDto(
    Guid Id,
    Guid EncounterId,
    Guid PrescribedByDoctorId,
    string PrescriptionCode,
    string Status,
    string GeneralInstructions,
    DateTime? IssuedAtUtc,
    DateTime? PublishedAtUtc,
    IReadOnlyList<PrescriptionItemDto> Items);

