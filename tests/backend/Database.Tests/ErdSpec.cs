using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartHealthcare.Database.Tests;

/// <summary>Đọc docs/db/erd-v2.2.json và các quyết định đã duyệt trong docs/plans/db-erd-v2.2.md (mục 5.5, 8).</summary>
public static partial class ErdSpec
{
    public sealed record Column(string Name, string Type, string[] Keys, string Note);

    public static readonly IReadOnlyDictionary<string, (string Domain, Column[] Columns)> Entities = Load(out FkWithoutLine);

    private static readonly Dictionary<(string Table, string Column), string> FkWithoutLine;

    private static readonly Dictionary<string, string> FkAlias = new()
    {
        ["SessionQuotaId"] = "DoctorSessionSlotQuota", ["MedicationId"] = "MedicationCatalog",
        ["TargetStationId"] = "ServiceStation", ["CurrentStationId"] = "ServiceStation", ["StationId"] = "ServiceStation",
        ["CurrentTicketId"] = "QueueTicket", ["CurrentPatientId"] = "Patient", ["PayerAppUserId"] = "AppUser",
        ["PerformedByUserId"] = "AppUser", ["InitiatedByUserId"] = "AppUser", ["AssignedRoomId"] = "Room",
        ["DefaultRoomId"] = "Room", ["TargetRoomId"] = "Room", ["OriginalRoomId"] = "Room",
        ["OriginalDoctorId"] = "Doctor", ["UpdatedByDoctorId"] = "Doctor", ["PrescribedByDoctorId"] = "Doctor",
        ["SelectedDoctorId"] = "Doctor", ["SelectedDoctorRankId"] = "DoctorRank", ["RecordedByStaffId"] = "Staff",
        ["PerformedByStaffId"] = "Staff", ["ApprovedByStaffId"] = "Staff", ["DecisionByStaffId"] = "Staff",
        ["OriginalPaymentTransactionId"] = "PaymentTransaction", ["RescheduledFromAppointmentId"] = "Appointment",
    };

    /// <summary>Thời điểm của sự kiện chưa xảy ra (kế hoạch mục 5.5).</summary>
    private static readonly HashSet<string> EventTimeNullable =
    [
        "PatientRepresentative.RevokedAtUtc", "RoomDeviceSession.LogoutAtUtc", "ActiveBookingClaim.ReleasedAtUtc",
        "Appointment.CancelledAtUtc", "QueueTicket.CalledAtUtc", "QueueTicket.ServedAtUtc", "QueueTicket.CompletedAtUtc",
        "Encounter.CompletedAtUtc", "ServiceOrderItem.CompletedAtUtc", "CLSResult.ApprovedAtUtc", "CLSResult.PublishedAtUtc",
        "Prescription.IssuedAtUtc", "PaymentTransaction.VerifiedAtUtc", "RefundRequest.DecidedAtUtc",
        "PaymentEvent.ProcessedAtUtc", "Operation.CompletedAtUtc", "OutboxMessage.ProcessedAtUtc",
    ];

    /// <summary>Cột trưởng nhóm duyệt nullable (kế hoạch mục 8.1).</summary>
    private static readonly HashSet<string> ApprovedNullable =
    [
        "CLSResult.ApprovedByStaffId", "CLSResult.PerformedAtUtc", "BookingAttempt.SelectedServiceDate",
        "Operation.FailureReason", "Appointment.CancellationReason", "ApiIdempotencyRecord.ResponseStatusCode",
        "ApiIdempotencyRecord.ResponseBodyJson", "PaymentTransaction.ProviderTransactionId",
        "RefundTransaction.ProviderRefundTransactionId", "PaymentTransaction.ProviderRawData",
        "RefundTransaction.ProviderResponsePayload", "AppUser.Email", "AppUser.PhoneNumber", "Patient.NationalId",
        "PatientRepresentative.AuthorizationProofDoc", "Doctor.Biography", "Specialty.Description", "PriceList.EffectiveToUtc",
        "PrescriptionItem.UsageInstructions", "Prescription.GeneralInstructions", "EncounterDiagnosis.ICD10Code",
        "EncounterDiagnosis.ClinicalNotes", "EncounterClinicalRecord.ChiefComplaint", "EncounterClinicalRecord.Symptoms",
        "EncounterClinicalRecord.MedicalHistory", "EncounterClinicalRecord.ClinicalExaminationNotes",
        "EncounterClinicalRecord.InitialClinicalImpression", "EncounterClinicalRecord.PostCLSConclusion",
        "EncounterClinicalRecord.TreatmentPlan", "EncounterClinicalRecord.FollowUpInstructions", "CLSResult.ResultSummary",
        "CLSResult.DetailedFindings", "AuditLog.DeviceId", "AuditLog.ClientIpAddress", "AuditLog.ChangedColumns",
        "AuditLog.OldValuesJson", "AuditLog.NewValuesJson", "AuditLog.ChangeReason",
    ];

    /// <summary>Văn bản dài hoặc JSON được phép nvarchar(max).</summary>
    public static bool IsLongText(string column) =>
        column.EndsWith("Json") || (column.Contains("Payload") && column != "PayloadHash") || column is "ProviderRawData"
            or "ClinicalNotes" or "DetailedFindings" or "ChiefComplaint" or "Symptoms" or "MedicalHistory"
            or "ClinicalExaminationNotes" or "InitialClinicalImpression" or "PostCLSConclusion" or "TreatmentPlan"
            or "FollowUpInstructions";

    public static readonly HashSet<string> VitalDecimals = ["WeightKg", "HeightCm", "SystolicBP", "DiastolicBP", "BodyTemperature", "SpO2"];

    public sealed record IndexSpec(string Table, string[] Columns, bool Unique, string? Filter, string? Name);

    public static readonly IndexSpec[] CompositeIndexes =
    [
        new("DoctorSessionSlotQuota", ["DoctorId", "ServiceDate"], true, null, null),
        new("ActiveBookingClaim", ["DoctorId", "ServiceDate", "PatientId"], true, "[IsActive] = 1", null),
        new("ApiIdempotencyRecord", ["CallerScope", "OperationType", "IdempotencyKey"], true, null, "UK_Idem"),
        new("PaymentEvent", ["Provider", "EventId"], true, null, "UK_ProviderEvent"),
        new("PaymentEvent", ["PayloadHash"], false, null, null),
        new("Appointment", ["RescheduledFromAppointmentId"], true, "[RescheduledFromAppointmentId] IS NOT NULL", null),
    ];

    /// <summary>CHECK ngoài enum, lấy từ note và 4 quyết định đã chốt.</summary>
    public static readonly (string Table, string Name)[] ExtraChecks =
    [
        ("DoctorSessionSlotQuota", "CK_DoctorSessionSlotQuota_HeldCount"),
        ("DoctorSessionSlotQuota", "CK_DoctorSessionSlotQuota_AdmittedCount"),
        ("DoctorSessionSlotQuota", "CK_DoctorSessionSlotQuota_LockedCancelledCount"),
        ("DoctorSessionSlotQuota", "CK_DoctorSessionSlotQuota_Capacity"),
        ("PriceListItem", "CK_PriceListItem_UnitPrice"),
        ("PriceListItem", "CK_PriceListItem_ItemTypeTarget"),
        ("PaymentAllocation", "CK_PaymentAllocation_AllocatedAmount"),
        ("BillingItem", "CK_BillingItem_TotalAmount"),
        ("RefundRequest", "CK_RefundRequest_PolicyRule"),
    ];

    public static bool IsNullable(string table, Column c) =>
        c.Note.StartsWith("Nullable", StringComparison.OrdinalIgnoreCase)
        || EventTimeNullable.Contains($"{table}.{c.Name}") || ApprovedNullable.Contains($"{table}.{c.Name}");

    public static string? EnumValues(Column c)
    {
        var head = c.Note.Split(" - ")[0].Trim();
        return c.Type == "string" && EnumList().IsMatch(head) ? head : null;
    }

    public static string FkTarget(string table, string column) =>
        FkWithoutLine.TryGetValue((table, column), out var p) ? p
        : FkAlias.TryGetValue(column, out p) ? p
        : column[..^2];

    private static Dictionary<string, (string, Column[])> Load(out Dictionary<(string, string), string> fkWithoutLine)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(FindErd()));
        var root = doc.RootElement;
        fkWithoutLine = root.GetProperty("fk_without_drawn_line").EnumerateArray()
            .ToDictionary(x => (x[0].GetString()!, x[1].GetString()!), x => x[2].GetString()!);
        return root.GetProperty("entities").EnumerateObject().ToDictionary(
            e => e.Name,
            e => (e.Value.GetProperty("domain").GetString()!,
                e.Value.GetProperty("columns").EnumerateArray().Select(c => new Column(
                    c.GetProperty("name").GetString()!, c.GetProperty("type").GetString()!,
                    c.GetProperty("keys").EnumerateArray().Select(k => k.GetString()!).ToArray(),
                    c.GetProperty("note").GetString()!)).ToArray()));
    }

    private static string FindErd()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "docs", "db", "erd-v2.2.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Không tìm thấy docs/db/erd-v2.2.json");
    }

    [GeneratedRegex(@"^[A-Za-z0-9]+(, [A-Za-z0-9]+)*$")]
    private static partial Regex EnumList();
}
