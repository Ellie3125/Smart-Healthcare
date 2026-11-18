using System.Text;
using DocumentsReporting.Application;
using DocumentsReporting.Contracts;
using DocumentsReporting.Infrastructure.Storage;
using DocumentsReporting.Tests.Mocks;
using IdentityPatients.Contracts;
using Scheduling.Contracts;
using Xunit;

namespace DocumentsReporting.Tests;

public sealed class PublishAndDownloadTests
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

    private static MemoryStream CreateValidPdfStream() =>
        new(Encoding.UTF8.GetBytes("%PDF-1.7\nSample content for medical result PDF"));

    [Fact]
    public async Task Download_UnpublishedFile_DeniedForPatient()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));

        // Tệp mới upload IsPublished = false
        var patientUserId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, patientUserId));
        Assert.Equal(DocumentsErrorCode.ResultNotPublished, ex.ErrorCode);
    }

    [Fact]
    public async Task Download_PublishedFile_DoctorAllowedImmediately()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));

        // Phát hành tệp
        await service.MarkPublishedAsync(uploaded.FileId);

        // Bác sĩ khám ban đầu là doctorId
        _clinical.IsOriginalDoctorHandler = (dId, encId) => Task.FromResult(dId == doctorId);
        // Giả sử bác sĩ CHƯA kết luận sau CLS
        _clinical.IsPostClsConclusionPublishedHandler = _ => Task.FromResult(false);

        // Bác sĩ vẫn xem được ngay khi Published
        var result = await service.OpenForDownloadAsync(uploaded.FileId, doctorId);
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("test.pdf", result.FileName);

        using var reader = new StreamReader(result.Content);
        var text = await reader.ReadToEndAsync();
        Assert.Contains("%PDF-1.7", text);
    }

    [Fact]
    public async Task Download_PublishedFile_PatientPendingVerification_Denied()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        // Không phải bác sĩ
        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        // Hồ sơ chưa Verified (Pending) -> CanAccessPatientAsync trả false
        _identity.CanAccessHandler = (uId, pId, level) => Task.FromResult(false);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, patientUserId));
        Assert.Equal(DocumentsErrorCode.AccessDenied, ex.ErrorCode);
    }

    [Fact]
    public async Task Download_PublishedFile_NoRepresentativeRelationship_Denied()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var strangerUserId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        _identity.CanAccessHandler = (uId, pId, level) => Task.FromResult(false);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, strangerUserId));
        Assert.Equal(DocumentsErrorCode.AccessDenied, ex.ErrorCode);
    }

    [Fact]
    public async Task Download_PublishedFile_PatientDeniedBeforeDoctorConclusion()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        // Hồ sơ đã Verified
        _identity.CanAccessHandler = (uId, pId, level) => Task.FromResult(true);
        // Bác sĩ CHƯA phát hành kết luận sau CLS
        _clinical.IsPostClsConclusionPublishedHandler = _ => Task.FromResult(false);

        var ex = await Assert.ThrowsAsync<DocumentsException>(() => service.OpenForDownloadAsync(uploaded.FileId, patientUserId));
        Assert.Equal(DocumentsErrorCode.DoctorNotConcluded, ex.ErrorCode);
    }

    [Fact]
    public async Task Download_PublishedFile_PatientAllowedAfterDoctorConclusion()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var patientUserId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));
        await service.MarkPublishedAsync(uploaded.FileId);

        _clinical.IsOriginalDoctorHandler = (_, _) => Task.FromResult(false);
        // Hồ sơ đã Verified
        _identity.CanAccessHandler = (uId, pId, level) => Task.FromResult(true);
        // Bác sĩ ĐÃ phát hành kết luận sau CLS
        _clinical.IsPostClsConclusionPublishedHandler = _ => Task.FromResult(true);

        var result = await service.OpenForDownloadAsync(uploaded.FileId, patientUserId);
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("test.pdf", result.FileName);
    }

    [Fact]
    public async Task RoomAccount_CannotViewPublishedFiles()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var stream = CreateValidPdfStream();
        var uploaded = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "test.pdf", "application/pdf", stream));

        // Khi chưa publish: phòng thấy tệp draft
        var draftsBefore = await service.GetAttachmentsByQueueTicketIdAsync(ticketId, roomId);
        Assert.Single(draftsBefore);

        // Sau khi phát hành:
        await service.MarkPublishedAsync(uploaded.FileId);

        // Phòng không còn thấy tệp trong danh sách draft của phòng
        var draftsAfter = await service.GetAttachmentsByQueueTicketIdAsync(ticketId, roomId);
        Assert.Empty(draftsAfter);
    }

    [Fact]
    public async Task MarkClsResultFilesPublished_PublishesAllFilesForClsResult()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var clsResultId = Guid.NewGuid();

        // Gán CLSResultId vào vé
        _scheduling.ValidateTicketHandler = (tId, rId) => Task.FromResult<ClsQueueTicketValidationDto?>(new ClsQueueTicketValidationDto(
            tId, rId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), clsResultId, "T-01", "Test", null, true, false));

        using var s1 = CreateValidPdfStream();
        using var s2 = CreateValidPdfStream();
        var f1 = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "file1.pdf", "application/pdf", s1));
        var f2 = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "file2.pdf", "application/pdf", s2));

        Assert.False(f1.IsPublished);
        Assert.False(f2.IsPublished);

        // Clinical phát hành kết quả
        await service.MarkClsResultFilesPublishedAsync(clsResultId);

        var pubFiles = await service.GetPublishedAttachmentsByClsResultIdAsync(clsResultId);
        Assert.Equal(2, pubFiles.Count);
        Assert.All(pubFiles, f => Assert.True(f.IsPublished));
    }

    [Fact]
    public async Task AmendPublishedAttachment_CreatesNewVersion_PreservesOriginal()
    {
        var service = CreateService();
        var ticketId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        using var s1 = CreateValidPdfStream();
        var original = await service.UploadResultFileAsync(new UploadResultFile(ticketId, roomId, "original.pdf", "application/pdf", s1));
        await service.MarkPublishedAsync(original.FileId);

        // Đính chính sau phát hành
        using var s2 = CreateValidPdfStream();
        var amendCmd = new UploadResultFile(ticketId, roomId, "corrected.pdf", "application/pdf", s2);
        var corrected = await service.AmendPublishedAttachmentAsync(original.FileId, amendCmd, "Bổ sung hình ảnh siêu âm góc nghiêng");

        Assert.NotNull(corrected);
        Assert.Equal(2, corrected.AttachmentVersion);
        Assert.NotEqual(original.FileId, corrected.FileId);

        // Bản gốc vẫn còn nguyên vẹn trong DB
        var originalInDb = await _store.FindByIdAsync(original.FileId);
        Assert.NotNull(originalInDb);
        Assert.Equal(1, originalInDb.AttachmentVersion);
        Assert.Equal("original.pdf", originalInDb.OriginalFileName);

        // Kiểm tra audit log có ghi nhận đính chính
        Assert.Contains(_auditLog.Logs, l => l.ActionType == "UPDATE" && l.ChangeReason == "Bổ sung hình ảnh siêu âm góc nghiêng");
    }
}
