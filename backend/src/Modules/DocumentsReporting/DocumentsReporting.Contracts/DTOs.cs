using System.Text.Json.Serialization;

namespace DocumentsReporting.Contracts;

public sealed record UploadResultFile(
    Guid QueueTicketId,
    Guid RoomId,
    string FileName,
    string ContentType,
    Stream Content,
    Guid? RoomAccountId = null,
    string? DeviceId = null,
    Guid? RoomDeviceSessionId = null,
    Guid? ResultId = null,
    Guid? PatientId = null);

public sealed record StoredFileDto
{
    [JsonConstructor]
    public StoredFileDto(
        Guid fileId,
        Guid queueTicketId,
        Guid encounterId,
        Guid patientId,
        Guid clsResultId,
        string fileName,
        string contentType,
        long sizeBytes,
        string sha256,
        string uploadStatus,
        bool isPublished,
        DateTime? uploadedAtUtc,
        int attachmentVersion)
    {
        FileId = fileId;
        QueueTicketId = queueTicketId;
        EncounterId = encounterId;
        PatientId = patientId;
        CLSResultId = clsResultId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        UploadStatus = uploadStatus;
        IsPublished = isPublished;
        UploadedAtUtc = uploadedAtUtc;
        AttachmentVersion = attachmentVersion;
    }

    public StoredFileDto(Guid fileId, string uploadStatus, string sha256, long sizeBytes)
        : this(fileId, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, string.Empty, sizeBytes, sha256, uploadStatus, false, null, 1)
    {
    }

    public Guid FileId { get; init; }
    public Guid QueueTicketId { get; init; }
    public Guid EncounterId { get; init; }
    public Guid PatientId { get; init; }
    public Guid CLSResultId { get; init; }
    public string FileName { get; init; }
    public string ContentType { get; init; }
    public long SizeBytes { get; init; }
    public string Sha256 { get; init; }
    public string UploadStatus { get; init; }
    public bool IsPublished { get; init; }
    public DateTime? UploadedAtUtc { get; init; }
    public int AttachmentVersion { get; init; }
}

public sealed record DocumentFileStreamResult(
    Stream Content,
    string ContentType,
    string FileName,
    long SizeBytes = 0);

public sealed record ResultAttachmentDto(
    Guid Id,
    Guid CLSResultId,
    Guid QueueTicketId,
    Guid EncounterId,
    Guid PatientId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    int AttachmentVersion,
    string UploadStatus,
    bool IsPublished,
    DateTime? UploadedAtUtc)
{
    public Guid FileId => Id;
    public string MimeType => ContentType;
    public DateTime? UploadedAt => UploadedAtUtc;
}

// =================== CÁC DTO CHUẨN BỊ CHO TUẦN 4: GHÉP LỊCH SỬ KHÁM & BÁO CÁO ===================

/// <summary>
/// DTO tóm tắt lượt khám phục vụ danh sách lịch sử khám trên kênh bệnh nhân (FR08, GP §5.4, §8.2).
/// </summary>
public sealed record PatientEncounterSummaryDto(
    Guid Id,
    string EncounterCode,
    DateTime ServiceDate,
    string DoctorName,
    string SpecialtyName,
    string? PrimaryDiagnosis,
    string Status,
    bool HasPrescription,
    bool HasPublishedAttachments,
    string? PatientName = null,
    Guid? PatientProfileId = null,
    bool? IsPublished = null,
    bool? HasClsResults = null);

/// <summary>
/// DTO chi tiết lượt khám tổng hợp từ Clinical + Billing + Documents (FR08, GP §5.4, §8.2).
/// Bệnh án phải Published; kết quả CLS chỉ trả sau khi bác sĩ ban đầu phát hành kết luận sau CLS.
/// </summary>
public sealed record PatientEncounterDetailDto(
    Guid Id,
    string EncounterCode,
    DateTime ServiceDate,
    string DoctorName,
    string SpecialtyName,
    string? DoctorRankName,
    string? RoomName,
    string? Symptoms,
    string? ClinicalNotes,
    EncounterVitalsDto? Vitals,
    IReadOnlyList<EncounterDiagnosisDto> Diagnoses,
    string? Conclusion,
    string? FollowUpInstruction,
    DateTime? PublishedAtUtc,
    int Version,
    bool IsPublished,
    bool HasPrescription,
    IReadOnlyList<EncounterClsResultDto> ClsResults,
    EncounterBillingSummaryDto? BillingSummary,
    string? PatientName = null);

public sealed record EncounterVitalsDto(
    decimal? SystolicBp,
    decimal? DiastolicBp,
    decimal? HeartRate,
    decimal? TemperatureC,
    decimal? Spo2,
    decimal? WeightKg,
    decimal? HeightCm);

public sealed record EncounterDiagnosisDto(
    string IcdCode,
    string Description,
    bool IsPrimary,
    string? DiagnosisType = null)
{
    public string Icd10Code => IcdCode;
    public string DiagnosisName => Description;
}

public sealed record EncounterClsResultDto(
    Guid Id,
    string ServiceCode,
    string ServiceName,
    string Status,
    string? SummaryText,
    string? TechnicalConclusion,
    IReadOnlyList<ResultAttachmentDto> Attachments);

/// <summary>
/// 5 chỉ tiêu tài chính chuẩn theo Phạm vi nghiệp vụ §5.5 & Billing.Contracts.
/// Documents gọi contract IBillingModule.GetEncounterCostSummaryAsync và ánh xạ, không tự tính lại.
/// </summary>
public sealed record EncounterBillingSummaryDto(
    Guid EncounterId,
    string Currency,
    decimal TotalPayable,
    decimal TotalPaid,
    decimal TotalRefunded,
    decimal NetCollected,
    decimal Outstanding,
    decimal CreditPendingRefund);

/// <summary>
/// DTO báo cáo vận hành tổng hợp (FR24, GP §8.2) kênh Staff (AdminOnly).
/// Báo cáo chỉ gồm số liệu tổng hợp, không chứa dữ liệu nhạy cảm hay nội dung chuyên môn bệnh án.
/// </summary>
public sealed record OperationalReportSummaryDto(
    DateOnly DateFrom,
    DateOnly DateTo,
    int TotalAppointments,
    int CheckedInCount,
    int CancelledCount,
    int AbsentCount,
    double AverageWaitTimeMinutes,
    int OverdueOperationsCount,
    int PaidUnallocatedCount,
    decimal TotalRevenue,
    decimal TotalRefunded);
