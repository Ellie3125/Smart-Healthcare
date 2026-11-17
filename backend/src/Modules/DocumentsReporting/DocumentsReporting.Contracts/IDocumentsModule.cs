namespace DocumentsReporting.Contracts;

/// <summary>
/// Public contract cho module DocumentsReporting (GP §6.4, §6.5, FR21, MED-06).
/// Quy tắc nghiệp vụ:
/// - Kết quả CLS chỉ do tài khoản phòng CLS (RoomAccount) tải lên theo vé của phòng.
/// - Upload chạy NGOÀI transaction DB để tránh giữ kết nối khi stream tệp lớn.
/// - Tệp chưa phát hành (IsPublished = false) bị từ chối khi tải về bởi bệnh nhân/người đại diện.
/// - Sau khi phát hành (Published), chỉ bác sĩ khám ban đầu, bệnh nhân và người đại diện Verified được xem.
/// - Bác sĩ khám ban đầu xem được ngay khi Published; bệnh nhân/người đại diện chỉ xem sau khi bác sĩ khám ban đầu phát hành kết luận sau CLS.
/// - Tài khoản phòng CLS không xem lại tệp đã Published; chỉ thấy tệp Draft của chính phòng mình.
/// - Backend stream tệp trực tiếp từ R2, tuyệt đối không trả URL Cloudflare R2 cho client.
/// </summary>
public interface IDocumentsModule
{
    /// <summary>
    /// Tải tệp kết quả CLS lên lưu trữ R2 và tạo bản ghi đính kèm kết quả.
    /// QUY TẮC: Phải được gọi NGOÀI transaction database.
    /// Chỉ tài khoản phòng CLS (RoomAccount) gắn với RoomId của vé mới có quyền gọi.
    /// Request chỉ nhận QueueTicketId và tệp; server tự tra EncounterId và PatientId từ vé.
    /// </summary>
    Task<StoredFileDto> UploadResultFileAsync(UploadResultFile command, CancellationToken ct = default);

    /// <summary>
    /// Đánh dấu một tệp kết quả đã được phát hành (Published).
    /// </summary>
    Task MarkPublishedAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Đánh dấu toàn bộ tệp kết quả thuộc một CLSResult là Published.
    /// QUY TẮC: Phải được gọi TRONG unit of work của module Clinical khi phát hành kết quả CLS
    /// (cùng DbConnection/DbTransaction, Documents không gọi ngược Clinical).
    /// </summary>
    Task MarkClsResultFilesPublishedAsync(Guid clsResultId, CancellationToken ct = default);

    /// <summary>
    /// Mở luồng stream tệp từ R2 để tải về, có kiểm tra quyền nghiêm ngặt.
    /// Bác sĩ ban đầu xem được ngay khi Published; bệnh nhân/người đại diện xem sau khi bác sĩ ban đầu kết luận sau CLS.
    /// Phòng CLS không xem lại tệp đã Published.
    /// </summary>
    Task<DocumentFileStreamResult> OpenForDownloadAsync(Guid fileId, Guid requesterUserId, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách tệp đính kèm theo vé (phòng CLS xem danh sách Draft của phòng mình).
    /// </summary>
    Task<IReadOnlyList<ResultAttachmentDto>> GetAttachmentsByQueueTicketIdAsync(Guid queueTicketId, Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách tệp đính kèm đã phát hành theo CLSResult.
    /// </summary>
    Task<IReadOnlyList<ResultAttachmentDto>> GetPublishedAttachmentsByClsResultIdAsync(Guid clsResultId, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách toàn bộ tệp đính kèm đã phát hành theo EncounterId (phục vụ ghép hồ sơ bệnh án).
    /// </summary>
    Task<IReadOnlyList<ResultAttachmentDto>> GetPublishedAttachmentsByEncounterAsync(Guid encounterId, CancellationToken ct = default);

    /// <summary>
    /// Phòng xóa tệp Draft của phòng mình trước khi trả kết quả.
    /// </summary>
    Task<bool> DeleteDraftAttachmentAsync(Guid fileId, Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Đính chính sau khi phát hành: tạo bản ghi phiên bản mới (AttachmentVersion + 1), giữ bản cũ.
    /// </summary>
    Task<StoredFileDto> AmendPublishedAttachmentAsync(Guid originalFileId, UploadResultFile command, string amendReason, CancellationToken ct = default);
}
