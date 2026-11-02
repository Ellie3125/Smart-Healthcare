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
}
