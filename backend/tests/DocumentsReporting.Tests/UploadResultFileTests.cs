using System.Text;
using DocumentsReporting.Application;
using DocumentsReporting.Contracts;
using DocumentsReporting.Domain.Enums;
using DocumentsReporting.Infrastructure.Storage;
using DocumentsReporting.Tests.Mocks;
using Scheduling.Contracts;
using Xunit;

namespace DocumentsReporting.Tests;

public sealed class UploadResultFileTests
{
    private readonly TestResultAttachmentStore _store = new();
    private readonly InMemoryObjectStorage _storage = new();
    private readonly TestSchedulingModule _scheduling = new();
    private readonly TestClinicalModule _clinical = new();
    private readonly TestIdentityPatientsModule _identity = new();
    private readonly TestAuditLogPort _auditLog = new();
    private readonly TestTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private DocumentsService CreateService() =>
        new(_store, _storage, _scheduling, _clinical, _identity, _auditLog, _clock);

    private static MemoryStream CreateValidPdfStream()
    {
        // PDF header "%PDF-1.7"
        var bytes = Encoding.UTF8.GetBytes("%PDF-1.7\nSample content for medical result PDF");
        return new MemoryStream(bytes);
    }

    private static MemoryStream CreateValidJpegStream()
    {
        // JPEG magic bytes: FF D8 FF E0
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        return new MemoryStream(bytes);
    }

    private static MemoryStream CreateValidPngStream()
    {
        // PNG magic bytes: 89 50 4E 47 0D 0A 1A 0A
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        return new MemoryStream(bytes);
    }

    private static MemoryStream CreateExeDisguisedAsPdfStream()
    {
        // Windows PE executable header: "MZ" (0x4D, 0x5A)
        var bytes = Encoding.ASCII.GetBytes("MZ\x90\x00\x03\x00\x00\x00FakeEXEContentRenamedToPdf");
        return new MemoryStream(bytes);
    }

    [Fact]
    public async Task Upload_ValidPdf_SucceedsAndSetsUploaded()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var command = new UploadResultFile(
            QueueTicketId: ticketId,
            RoomId: roomId,
            FileName: "ket_qua_sieu_am.pdf",
            ContentType: "application/pdf",
            Content: stream,
            RoomAccountId: Guid.NewGuid(),
            DeviceId: "DEVICE-ROOM-01");

        var result = await service.UploadResultFileAsync(command);

        Assert.NotNull(result);
        Assert.Equal("Uploaded", result.UploadStatus);
        Assert.False(result.IsPublished);
        Assert.NotNull(result.UploadedAtUtc);
        Assert.NotEmpty(result.Sha256);
        Assert.True(result.SizeBytes > 0);

        // Kiểm tra đối tượng đã lên storage
        var inStore = await _store.FindByIdAsync(result.FileId);
        Assert.NotNull(inStore);
        Assert.True(await _storage.ExistsAsync(inStore.R2StorageKey));

        // Kiểm tra audit log
        Assert.Contains(_auditLog.Logs, l => l.RecordId == result.FileId && l.ActionType == "INSERT");
    }

    [Fact]
    public async Task Upload_ValidJpegAndPng_Succeeds()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var jpegStream = CreateValidJpegStream();
        var jpegCmd = new UploadResultFile(ticketId, roomId, "xquang.jpg", "image/jpeg", jpegStream);
        var jpegRes = await service.UploadResultFileAsync(jpegCmd);
        Assert.Equal("image/jpeg", jpegRes.ContentType);

        using var pngStream = CreateValidPngStream();
        var pngCmd = new UploadResultFile(ticketId, roomId, "xquang2.png", "image/png", pngStream);
        var pngRes = await service.UploadResultFileAsync(pngCmd);
        Assert.Equal("image/png", pngRes.ContentType);
    }

    [Fact]
    public async Task Upload_ExeRenamedToPdf_ThrowsInvalidFileTypeException()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var stream = CreateExeDisguisedAsPdfStream();
        var command = new UploadResultFile(
            ticketId,
            roomId,
            "ketqua_virus.pdf", // Tên là .pdf nhưng ruột là .exe
            "application/pdf",
            stream);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.UploadResultFileAsync(command));
        Assert.Equal(DocumentsErrorCode.InvalidFileType, ex.ErrorCode);
    }

    [Fact]
    public async Task Upload_Exceeding20MB_ThrowsFileTooLargeException()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        // Tạo stream vượt quá 20MB
        var largeStream = new MemoryStream();
        // Ghi magic bytes PDF
        largeStream.Write(Encoding.UTF8.GetBytes("%PDF-1.7\n"));
        // Đặt kích thước 21 MB
        largeStream.SetLength(21 * 1024 * 1024);
        largeStream.Position = 0;

        var command = new UploadResultFile(ticketId, roomId, "giant_file.pdf", "application/pdf", largeStream);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.UploadResultFileAsync(command));
        Assert.Equal(DocumentsErrorCode.FileTooLarge, ex.ErrorCode);
    }

    [Fact]
    public async Task Upload_TicketOfOtherRoom_ThrowsTicketRoomMismatchException()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var actualRoomId = Guid.NewGuid();
        var otherRoomId = Guid.NewGuid();

        // Vé thuộc actualRoomId
        _scheduling.ValidateTicketHandler = (tId, rId) => Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            QueueTicketId: tId,
            RoomId: actualRoomId,
            EncounterId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            ServiceOrderItemId: Guid.NewGuid(),
            CLSResultId: Guid.NewGuid(),
            TicketNumber: "T-01",
            PatientName: "Test",
            PatientDateOfBirth: null,
            IsServiceFinished: true,
            IsResultReturned: false));

        using var stream = CreateValidPdfStream();
        // Tài khoản của otherRoomId gọi tải lên
        var command = new UploadResultFile(ticketId, otherRoomId, "kq.pdf", "application/pdf", stream);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.UploadResultFileAsync(command));
        Assert.Equal(DocumentsErrorCode.TicketRoomMismatch, ex.ErrorCode);
    }

    [Fact]
    public async Task Upload_IgnoresClientProvidedPatientIdAndEncounterId()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var serverPatientId = Guid.NewGuid();
        var serverEncounterId = Guid.NewGuid();

        _scheduling.ValidateTicketHandler = (tId, rId) => Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            QueueTicketId: tId,
            RoomId: rId,
            EncounterId: serverEncounterId,
            PatientId: serverPatientId,
            ServiceOrderItemId: Guid.NewGuid(),
            CLSResultId: Guid.NewGuid(),
            TicketNumber: "T-01",
            PatientName: "Test",
            PatientDateOfBirth: null,
            IsServiceFinished: true,
            IsResultReturned: false));

        using var stream = CreateValidPdfStream();
        // Client cố tình gửi PatientId và EncounterId khác
        var spoofedPatientId = Guid.NewGuid();
        var spoofedEncounterId = Guid.NewGuid();

        var command = new UploadResultFile(
            QueueTicketId: ticketId,
            RoomId: roomId,
            FileName: "kq.pdf",
            ContentType: "application/pdf",
            Content: stream,
            PatientId: spoofedPatientId);

        var result = await service.UploadResultFileAsync(command);

        // Server phải lấy từ vé, không nhận từ client
        Assert.Equal(serverPatientId, result.PatientId);
        Assert.Equal(serverEncounterId, result.EncounterId);
        Assert.NotEqual(spoofedPatientId, result.PatientId);
    }

    [Fact]
    public async Task Upload_TicketNotFinishedOrAlreadyReturned_ThrowsInvalidTicketStatus()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        // 1. Vé chưa khám xong
        _scheduling.ValidateTicketHandler = (tId, rId) => Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            tId, rId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T-01", "Test", null,
            IsServiceFinished: false,
            IsResultReturned: false));

        using var stream1 = CreateValidPdfStream();
        var cmd1 = new UploadResultFile(ticketId, roomId, "kq.pdf", "application/pdf", stream1);
        var ex1 = await Assert.ThrowsAsync<DocumentsException>(() => service.UploadResultFileAsync(cmd1));
        Assert.Equal(DocumentsErrorCode.InvalidTicketStatus, ex1.ErrorCode);

        // 2. Vé đã trả kết quả
        _scheduling.ValidateTicketHandler = (tId, rId) => Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            tId, rId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T-01", "Test", null,
            IsServiceFinished: true,
            IsResultReturned: true));

        using var stream2 = CreateValidPdfStream();
        var cmd2 = new UploadResultFile(ticketId, roomId, "kq.pdf", "application/pdf", stream2);
        var ex2 = await Assert.ThrowsAsync<DocumentsException>(() => service.UploadResultFileAsync(cmd2));
        Assert.Equal(DocumentsErrorCode.InvalidTicketStatus, ex2.ErrorCode);
    }

    [Fact]
    public async Task Upload_FailsInStorage_KeepsDraft_RetryDoesNotDuplicate()
    {
        // Giả lập storage lỗi lần đầu
        var faultyStorage = new FaultyStorage();
        var service = new DocumentsService(_store, faultyStorage, _scheduling, _clinical, _identity, _auditLog, _clock);

        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        // Lần 1: Storage throw exception
        faultyStorage.ShouldThrow = true;
        using var stream1 = CreateValidPdfStream();
        var cmd1 = new UploadResultFile(ticketId, roomId, "kq_loi.pdf", "application/pdf", stream1);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.UploadResultFileAsync(cmd1));
        Assert.Equal(DocumentsErrorCode.StorageError, ex.ErrorCode);

        // Kiểm tra bản ghi vẫn tồn tại ở trạng thái Draft
        var draft = _store.AllAttachments.SingleOrDefault(x => x.QueueTicketId == ticketId && x.OriginalFileName == "kq_loi.pdf");
        Assert.NotNull(draft);
        Assert.Equal(UploadStatus.Draft, draft.UploadStatus);

        // Lần 2: Storage thành công -> gửi lại cùng tệp
        faultyStorage.ShouldThrow = false;
        using var stream2 = CreateValidPdfStream();
        var cmd2 = new UploadResultFile(ticketId, roomId, "kq_loi.pdf", "application/pdf", stream2);

        var result2 = await service.UploadResultFileAsync(cmd2);

        // Đảm bảo không tạo thêm bản ghi mới, mà cập nhật đúng bản ghi cũ
        Assert.Single(_store.AllAttachments.Where(x => x.QueueTicketId == ticketId && x.OriginalFileName == "kq_loi.pdf"));
        Assert.Equal(draft.Id, result2.FileId);
        Assert.Equal("Uploaded", result2.UploadStatus);
    }

    [Fact]
    public async Task RoomCanDeleteDraftBeforePublish_Succeeds()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var cmd = new UploadResultFile(ticketId, roomId, "xoa_nhap.pdf", "application/pdf", stream);
        var uploaded = await service.UploadResultFileAsync(cmd);

        // Phòng xóa tệp nháp của phòng mình
        var deleted = await service.DeleteDraftAttachmentAsync(uploaded.FileId, roomId);
        Assert.True(deleted);

        var inStore = await _store.FindByIdAsync(uploaded.FileId);
        Assert.NotNull(inStore);
        Assert.Equal(UploadStatus.Removed, inStore.UploadStatus);
        Assert.Contains(_auditLog.Logs, l => l.RecordId == uploaded.FileId && l.ActionType == "DELETE");
    }

    [Fact]
    public async Task RoomCannotDeleteOtherRoomDraft_ThrowsTicketRoomMismatch()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var room1 = Guid.NewGuid();
        var room2 = Guid.NewGuid();

        _scheduling.ValidateTicketHandler = (tId, rId) => Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            tId, room1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "T-01", "Test", null, true, false));

        using var stream = CreateValidPdfStream();
        var cmd = new UploadResultFile(ticketId, room1, "t1.pdf", "application/pdf", stream);
        var uploaded = await service.UploadResultFileAsync(cmd);

        // Phòng 2 cố xóa
        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.DeleteDraftAttachmentAsync(uploaded.FileId, room2));
        Assert.Equal(DocumentsErrorCode.TicketRoomMismatch, ex.ErrorCode);
    }

    [Fact]
    public async Task CannotDeletePublishedFile_ThrowsInvalidOperation()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var cmd = new UploadResultFile(ticketId, roomId, "t2.pdf", "application/pdf", stream);
        var uploaded = await service.UploadResultFileAsync(cmd);

        // Phát hành tệp
        await service.MarkPublishedAsync(uploaded.FileId);

        // Xóa tệp đã phát hành
        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.DeleteDraftAttachmentAsync(uploaded.FileId, roomId));
        Assert.Equal(DocumentsErrorCode.InvalidOperation, ex.ErrorCode);
    }

    private sealed class FaultyStorage : IObjectStorage
    {
        public bool ShouldThrow { get; set; }
        private readonly InMemoryObjectStorage _inner = new();

        public Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            if (ShouldThrow) throw new IOException("Simulated network failure on R2 upload");
            return _inner.UploadAsync(key, content, contentType, ct);
        }

        public Task<Stream> DownloadAsync(string key, CancellationToken ct = default) => _inner.DownloadAsync(key, ct);
        public Task DeleteAsync(string key, CancellationToken ct = default) => _inner.DeleteAsync(key, ct);
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => _inner.ExistsAsync(key, ct);
    }
}
