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

public sealed record FileStreamResult(
    Stream Content,
    string ContentType,
    string FileName);

/// <summary>
/// Contract quản lý lưu trữ và tải tệp kết quả cận lâm sàng (Cloudflare R2 private).
/// Bắt buộc tuân thủ MED-06 và SEC-01.
/// </summary>
public interface IDocumentsModule
{
    /// <summary>
    /// Tải tệp kết quả lên bộ lưu trữ R2 private.
    /// PHẢI gọi NGOÀI transaction cơ sở dữ liệu.
    /// CHỈ tài khoản phòng CLS (RoomAccount) mới được quyền upload theo vé của phòng.
    /// </summary>
    Task<StoredFileDto> UploadResultFileAsync(UploadResultFile command, CancellationToken ct = default);

    /// <summary>
    /// Đánh dấu tệp kết quả đã phát hành chính thức (Published).
    /// Được gọi BÊN TRONG Unit of Work từ module Clinical khi bác sĩ duyệt kết quả.
    /// </summary>
    Task MarkPublishedAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Mở luồng tải tệp để Backend kiểm quyền rồi proxy/stream trực tiếp cho client.
    /// KHÔNG BAO GIỜ trả về presigned URL của Cloudflare R2 ra client.
    /// QUY TẮC BẢO MẬT:
    /// - Tệp chưa Published bị từ chối tải (chỉ tài khoản phòng đang thực hiện thấy draft).
    /// - Sau khi Published: CHỈ bác sĩ khám ban đầu, bệnh nhân và người đại diện Verified mới được xem.
    ///   Tài khoản phòng CLS KHÔNG ĐƯỢC xem lại sau khi đã phát hành.
    /// </summary>
    Task<FileStreamResult> OpenForDownloadAsync(Guid fileId, Guid requesterUserId, CancellationToken ct = default);
}

