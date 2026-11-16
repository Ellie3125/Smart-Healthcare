using DocumentsReporting.Domain.Enums;

namespace DocumentsReporting.Domain.Entities;

public sealed class ResultAttachment
{
    public Guid Id { get; set; }
    // ID chéo module: clinical.CLSResult, không FK
    public Guid CLSResultId { get; set; }
    // ID chéo module: scheduling.QueueTicket, không FK
    public Guid QueueTicketId { get; set; }
    // ID chéo module: clinical.Encounter, không FK
    public Guid EncounterId { get; set; }
    // ID chéo module: identity.Patient, không FK
    public Guid PatientId { get; set; }
    public string R2StorageKey { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string FileContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = null!;
    public int AttachmentVersion { get; set; }
    public UploadStatus UploadStatus { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? UploadedAtUtc { get; set; }

    public static ResultAttachment CreateDraft(
        Guid id,
        Guid clsResultId,
        Guid queueTicketId,
        Guid encounterId,
        Guid patientId,
        string r2StorageKey,
        string originalFileName,
        string fileContentType,
        long fileSizeBytes,
        string sha256Hash,
        int version = 1)
    {
        return new ResultAttachment
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            CLSResultId = clsResultId,
            QueueTicketId = queueTicketId,
            EncounterId = encounterId,
            PatientId = patientId,
            R2StorageKey = r2StorageKey,
            OriginalFileName = originalFileName,
            FileContentType = fileContentType,
            FileSizeBytes = fileSizeBytes,
            Sha256Hash = sha256Hash,
            AttachmentVersion = version,
            UploadStatus = UploadStatus.Draft,
            IsPublished = false,
            UploadedAtUtc = null
        };
    }

    public void MarkUploaded(DateTime uploadedAtUtc)
    {
        UploadStatus = UploadStatus.Uploaded;
        UploadedAtUtc = uploadedAtUtc;
    }

    public void MarkPublished()
    {
        IsPublished = true;
        if (UploadStatus == UploadStatus.Draft)
        {
            UploadStatus = UploadStatus.Uploaded;
        }
    }

    public void MarkRemoved()
    {
        UploadStatus = UploadStatus.Removed;
    }

    public ResultAttachment CreateCorrection(
        Guid newId,
        string newR2StorageKey,
        string originalFileName,
        string fileContentType,
        long fileSizeBytes,
        string sha256Hash)
    {
        return new ResultAttachment
        {
            Id = newId == Guid.Empty ? Guid.NewGuid() : newId,
            CLSResultId = CLSResultId,
            QueueTicketId = QueueTicketId,
            EncounterId = EncounterId,
            PatientId = PatientId,
            R2StorageKey = newR2StorageKey,
            OriginalFileName = originalFileName,
            FileContentType = fileContentType,
            FileSizeBytes = fileSizeBytes,
            Sha256Hash = sha256Hash,
            AttachmentVersion = AttachmentVersion + 1,
            UploadStatus = UploadStatus.Draft,
            IsPublished = false,
            UploadedAtUtc = null
        };
    }
}
