namespace DocumentsReporting.Contracts;

public sealed record UploadResultFile(
    Guid ResultId,
    Guid PatientId,
    Guid RoomId,
    string FileName,
    string ContentType,
    Stream Content);

public sealed record StoredFileDto(
    Guid FileId,
    string UploadStatus,
    string Sha256,
    long SizeBytes);

public sealed record DocumentFileStreamResult(
    Stream Content,
    string ContentType,
    string FileName);

