namespace SmartHealthcare.Clinical.Domain;

public sealed class ResultAttachment
{
    public Guid Id { get; set; }
    public Guid CLSResultId { get; set; }
    public CLSResult CLSResult { get; set; } = null!;
    public string R2StorageKey { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string FileContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public int AttachmentVersion { get; set; }
    public bool IsDraft { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}
