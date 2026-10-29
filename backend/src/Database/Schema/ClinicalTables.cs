using Microsoft.EntityFrameworkCore;

namespace SmartHealthcare.Database.Schema;

/// <summary>Lượt khám, hồ sơ lâm sàng, chỉ định CLS, đơn thuốc và kết quả (sơ đồ 4).</summary>
internal static class ClinicalTables
{
    public static void Configure(ModelBuilder model)
    {
        model.Table("Encounter", t =>
        {
            t.Id();
            t.Ref("AppointmentId", "Appointment");
            t.Ref("PatientId", "Patient");
            t.Ref("OriginalDoctorId", "Doctor");
            t.Ref("OriginalRoomId", "Room");
            t.Req<string>("SpecialtySnapshot", "nvarchar(150)");
            t.Req<string>("DoctorRankSnapshot", "nvarchar(100)");
            t.Utc("CheckedInAtUtc");
            t.Enum("Status", 20, "InProgress", "WaitingCLS", "ReturningDoctor", "Completed", "Abandoned").Default("N'InProgress'");
            t.OptUtc("CompletedAtUtc");
            t.RowVersion();
            t.Unique("AppointmentId");
        });

        model.Table("EncounterClinicalRecord", t =>
        {
            t.Id();
            t.Ref("EncounterId", "Encounter");
            t.Enum("Status", 10, "Draft", "Published").Default("N'Draft'");
            t.Opt<string>("ChiefComplaint", "nvarchar(1000)");
            foreach (var column in new[] { "Symptoms", "MedicalHistory", "ClinicalExaminationNotes", "InitialClinicalImpression",
                                           "PostCLSConclusion", "TreatmentPlan", "FollowUpInstructions" })
                t.Opt<string>(column, "nvarchar(max)");
            t.Utc("RecordedAtUtc");
            t.OptUtc("PublishedAtUtc");
            t.Utc("LastUpdatedUtc");
            t.Ref("UpdatedByDoctorId", "Doctor");
            t.RowVersion();
            t.Unique("EncounterId");
        });

        model.Table("VitalSign", t =>
        {
            t.Id();
            t.Ref("EncounterId", "Encounter");
            t.Opt<decimal>("WeightKg", "decimal(5,2)");
            t.Opt<decimal>("HeightCm", "decimal(5,2)");
            t.Opt<decimal>("SystolicBP", "decimal(5,1)");
            t.Opt<decimal>("DiastolicBP", "decimal(5,1)");
            t.Opt<int>("PulseRate", "int");
            t.Opt<decimal>("BodyTemperature", "decimal(4,1)");
            t.Opt<decimal>("SpO2", "decimal(4,1)");
            t.Ref("RecordedByStaffId", "Staff");
            t.Utc("RecordedAtUtc");
            foreach (var column in new[] { "WeightKg", "HeightCm", "SystolicBP", "DiastolicBP", "PulseRate", "BodyTemperature" })
                t.Rule(column, $"{column} > 0");
            t.Rule("SpO2", "SpO2 BETWEEN 0 AND 100");
        });

        model.Table("EncounterDiagnosis", t =>
        {
            t.Id();
            t.Ref("EncounterId", "Encounter");
            t.Enum("DiagnosisType", 10, "Initial", "Final");
            t.Req<string>("ICD10Code", "varchar(10)");
            t.Req<string>("DiagnosisName", "nvarchar(300)");
            t.Flag("IsPrimary", false);
            t.Opt<string>("ClinicalNotes", "nvarchar(max)");
            t.Utc("DiagnosedAtUtc");
        });

        model.Table("ServiceOrder", t =>
        {
            t.Id();
            t.Ref("EncounterId", "Encounter");
            t.Req<int>("OrderRoundNumber", "int");
            t.Utc("OrderedAtUtc");
            t.Ref("PrescribedByDoctorId", "Doctor");
            t.Enum("OrderBatchStatus", 10, "Draft", "Confirmed", "Completed", "Cancelled").Default("N'Draft'");
            t.Flag("HasActiveReturnTicket", false);
            t.Rule("OrderRoundNumber", "OrderRoundNumber >= 1");
            t.UniqueKey("UQ_ServiceOrder_EncounterRound", "EncounterId", "OrderRoundNumber");
        });

        model.Table("ServiceOrderItem", t =>
        {
            t.Id();
            t.Ref("ServiceOrderId", "ServiceOrder");
            t.Ref("MedicalServiceId", "MedicalService");
            t.Req<string>("ServiceNameSnapshot", "nvarchar(200)");
            t.Money("AppliedPrice");
            t.Enum("ItemStatus", 20, "Ordered", "SampleCollected", "InProgress", "ServiceFinished", "Completed", "Cancelled")
                .Default("N'Ordered'");
            t.Flag("IsMandatory", false); // bắt buộc có KQ để kích hoạt vé quay lại
            t.OptUtc("CompletedAtUtc");
            t.Rule("AppliedPrice", "AppliedPrice >= 0");
        });

        model.Table("Prescription", t =>
        {
            t.Id();
            t.Ref("EncounterId", "Encounter");
            t.Ref("PrescribedByDoctorId", "Doctor");
            t.Req<string>("PrescriptionCode", "varchar(30)");
            t.Enum("Status", 10, "Draft", "Issued", "Cancelled").Default("N'Draft'");
            t.Opt<string>("GeneralInstructions", "nvarchar(max)");
            t.OptUtc("IssuedAtUtc");
            t.OptUtc("PublishedAtUtc");
            t.RowVersion();
            t.Unique("PrescriptionCode");
        });

        model.Table("PrescriptionItem", t =>
        {
            t.Id();
            t.Ref("PrescriptionId", "Prescription");
            t.Ref("MedicationId", "MedicationCatalog");
            t.Req<string>("MedicationNameSnapshot", "nvarchar(200)");
            t.Req<string>("ActiveIngredientSnapshot", "nvarchar(300)");
            t.Req<string>("Dosage", "nvarchar(100)");
            t.Enum("Route", 15, "Oral", "Topical", "Injection", "Inhalation");
            t.Req<string>("Frequency", "nvarchar(100)");
            t.Req<int>("DurationDays", "int");
            t.Req<int>("Quantity", "int");
            t.Opt<string>("UsageInstructions", "nvarchar(1000)");
            t.Rule("DurationDays", "DurationDays > 0");
            t.Rule("Quantity", "Quantity > 0");
        });

        model.Table("CLSResult", t =>
        {
            t.Id();
            t.Ref("ServiceOrderItemId", "ServiceOrderItem");
            t.Opt<string>("ResultSummary", "nvarchar(2000)");
            t.Opt<string>("DetailedFindings", "nvarchar(max)");
            t.Enum("ResultStatus", 10, "Draft", "Approved", "Published", "Cancelled").Default("N'Draft'");
            t.Ref("PerformedByStaffId", "Staff", "PerformedBy");   // KTV thực hiện
            t.OptRef("ApprovedByStaffId", "Staff", "ApprovedBy");  // bác sĩ chuyên môn duyệt
            t.Utc("PerformedAtUtc");
            t.OptUtc("ApprovedAtUtc");
            t.OptUtc("PublishedAtUtc");
            t.RowVersion();
            t.Unique("ServiceOrderItemId");
        });

        model.Table("ResultAttachment", t =>
        {
            t.Id();
            t.Ref("CLSResultId", "CLSResult");
            t.Req<string>("R2StorageKey", "varchar(500)");
            t.Req<string>("OriginalFileName", "nvarchar(255)");
            t.Req<string>("FileContentType", "varchar(100)");
            t.Req<long>("FileSizeBytes", "bigint");
            t.Req<int>("AttachmentVersion", "int").Default("1");
            t.Flag("IsDraft", true);
            t.Utc("UploadedAtUtc");
            t.Rule("FileSizeBytes", "FileSizeBytes > 0");
            t.Rule("AttachmentVersion", "AttachmentVersion >= 1");
            t.Unique("R2StorageKey");
        });
    }
}
