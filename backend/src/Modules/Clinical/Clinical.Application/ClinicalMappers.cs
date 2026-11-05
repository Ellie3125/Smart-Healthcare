using Clinical.Contracts;
using Clinical.Domain.Entities;

namespace Clinical.Application;

internal static class ClinicalMappers
{
    public static EncounterDto ToDto(Encounter e) => new(
        e.Id,
        e.AppointmentId,
        e.PatientId,
        e.OriginalDoctorId,
        e.OriginalRoomId,
        e.SpecialtySnapshot,
        e.DoctorRankSnapshot,
        e.CheckedInAtUtc,
        e.Status.ToString(),
        e.CompletedAtUtc);

    public static EncounterSummaryDto ToSummaryDto(Encounter e) => new(
        e.Id,
        e.AppointmentId,
        e.PatientId,
        e.SpecialtySnapshot,
        e.DoctorRankSnapshot,
        e.CheckedInAtUtc,
        e.Status.ToString(),
        e.CompletedAtUtc);

    public static VitalSignDto ToDto(VitalSign v) => new(
        v.Id,
        v.EncounterId,
        v.WeightKg,
        v.HeightCm,
        v.SystolicBP,
        v.DiastolicBP,
        v.PulseRate,
        v.BodyTemperature,
        v.SpO2,
        v.RecordedByStaffId,
        v.RecordedAtUtc);

    public static EncounterClinicalRecordDto ToDto(EncounterClinicalRecord r) => new(
        r.Id,
        r.EncounterId,
        r.Status.ToString(),
        r.ChiefComplaint,
        r.Symptoms,
        r.MedicalHistory,
        r.ClinicalExaminationNotes,
        r.InitialClinicalImpression,
        r.PostCLSConclusion,
        r.TreatmentPlan,
        r.FollowUpInstructions,
        r.RecordedAtUtc,
        r.PublishedAtUtc,
        r.LastUpdatedUtc,
        r.UpdatedByDoctorId);

    public static EncounterDiagnosisDto ToDto(EncounterDiagnosis d) => new(
        d.Id,
        d.EncounterId,
        d.DiagnosisType.ToString(),
        d.ICD10Code,
        d.DiagnosisName,
        d.IsPrimary,
        d.ClinicalNotes,
        d.DiagnosedAtUtc);

    public static CLSResultDto ToDto(CLSResult r) => new(
        r.Id,
        r.ServiceOrderItemId,
        r.ResultSummary,
        r.DetailedFindings,
        r.ResultStatus.ToString(),
        r.PerformedByStaffId,
        r.ApprovedByStaffId,
        r.PerformedAtUtc,
        r.ApprovedAtUtc,
        r.PublishedAtUtc);

    public static ServiceOrderItemDto ToDto(ServiceOrderItem item, CLSResult? result) => new(
        item.Id,
        item.ServiceOrderId,
        item.MedicalServiceId,
        item.ServiceNameSnapshot,
        item.AppliedPrice,
        item.ItemStatus.ToString(),
        item.IsMandatory,
        item.CompletedAtUtc,
        result is null ? null : ToDto(result));

    public static PrescriptionItemDto ToDto(PrescriptionItem item) => new(
        item.Id,
        item.PrescriptionId,
        item.MedicationId,
        item.MedicationNameSnapshot,
        item.ActiveIngredientSnapshot,
        item.Dosage,
        item.Route.ToString(),
        item.Frequency,
        item.DurationDays,
        item.Quantity,
        item.UsageInstructions);

    public static PrescriptionDto ToDto(Prescription p, IReadOnlyList<PrescriptionItem> items) => new(
        p.Id,
        p.EncounterId,
        p.PrescribedByDoctorId,
        p.PrescriptionCode,
        p.Status.ToString(),
        p.GeneralInstructions,
        p.IssuedAtUtc,
        p.PublishedAtUtc,
        items.Select(ToDto).ToList());
}

