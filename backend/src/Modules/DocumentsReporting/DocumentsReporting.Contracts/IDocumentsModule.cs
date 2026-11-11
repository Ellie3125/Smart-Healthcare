namespace DocumentsReporting.Contracts;

/// <summary>
/// Public contract cho module DocumentsReporting (GP §6.5, MED-06).
/// Quy tắc nghiệp vụ:
/// - Kết quả CLS chỉ do tài khoản phòng CLS (RoomAccount) tải lên theo vé của phòng.
/// - Tệp chưa phát hành (IsPublished = false) bị từ chối khi tải về bởi bệnh nhân/người đại diện.
/// - Sau khi phát hành (Published), chỉ bác sĩ khám ban đầu, bệnh nhân và người đại diện Verified được xem.
/// - Tài khoản phòng CLS không xem lại tệp đã trả.
/// - Backend kiểm quyền rồi stream tệp trực tiếp, không trả presigned URL của Cloudflare R2 cho client.
/// </summary>
public interface IDocumentsModule
{
    /// <summary>
    /// Tải tệp kết quả CLS lên lưu trữ R2 và tạo bản ghi đính kèm kết quả.
    /// QUY TẮC: Phải được gọi NGOÀI transaction database để tránh giữ kết nối và khóa DB khi stream tệp lớn.
    /// Chỉ tài khoản phòng CLS (RoomAccount) gắn với RoomId của chỉ định mới có quyền gọi.
    /// </summary>
    Task<StoredFileDto> UploadResultFileAsync(UploadResultFile command, CancellationToken ct = default);

    /// <summary>
    /// Đánh dấu tệp kết quả đã được phát hành (Published).
    /// QUY TẮC: Phải được gọi TRONG unit of work của module Clinical khi bác sĩ duyệt/phát hành kết luận hoặc kết quả CLS.
    /// </summary>
    Task MarkPublishedAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Mở luồng stream tệp để tải về.
    /// QUY TẮC: Backend kiểm tra quyền của requesterUserId (bác sĩ khám chính hoặc bệnh nhân/người đại diện Verified)
    /// trước khi mở stream từ R2. Phòng CLS không được xem lại sau khi đã tải lên.
    /// </summary>
    Task<DocumentFileStreamResult> OpenForDownloadAsync(Guid fileId, Guid requesterUserId, CancellationToken ct = default);
}

