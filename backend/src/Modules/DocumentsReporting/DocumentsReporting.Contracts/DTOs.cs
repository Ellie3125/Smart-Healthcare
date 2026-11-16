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
    DateTime? UploadedAtUtc);
