using System.Text.Json;
using System.Text.RegularExpressions;
using Clinical.Contracts;
using DocumentsReporting.Contracts;
using DocumentsReporting.Domain.Entities;
using DocumentsReporting.Domain.Enums;
using IdentityPatients.Contracts;
using Scheduling.Contracts;

namespace DocumentsReporting.Application;

public sealed class DocumentsService(
    IResultAttachmentStore store,
    IObjectStorage storage,
    ISchedulingModule schedulingModule,
    IClinicalModule clinicalModule,
    IIdentityPatientsModule identityPatientsModule,
    IAuditLogPort auditLogPort,
    TimeProvider timeProvider) : IDocumentsModule
{
    public async Task<StoredFileDto> UploadResultFileAsync(UploadResultFile command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.QueueTicketId == Guid.Empty)
            throw new DocumentsException(DocumentsErrorCode.TicketNotFound, "Mã vé không được để trống.");

        if (command.RoomId == Guid.Empty)
            throw new DocumentsException(DocumentsErrorCode.TicketRoomMismatch, "Mã phòng không được để trống.");

        // 1. Kiểm vé thuộc đúng RoomId của tài khoản phòng qua contract Scheduling
        var ticket = await schedulingModule.ValidateClsTicketForRoomAsync(command.QueueTicketId, command.RoomId, ct);
        if (ticket == null)
        {
            throw new DocumentsException(DocumentsErrorCode.TicketNotFound, "Không tìm thấy vé thực hiện CLS hợp lệ.");
        }

        if (ticket.RoomId != command.RoomId)
        {
            throw new DocumentsException(DocumentsErrorCode.TicketRoomMismatch, "Vé không thuộc phòng CLS của tài khoản hiện tại.");
        }

        // 2. Chỉ nhận vé đã ServiceFinished và chưa trả kết quả
        if (!ticket.IsServiceFinished || ticket.IsResultReturned)
        {
            throw new DocumentsException(DocumentsErrorCode.InvalidTicketStatus, "Chỉ nhận vé đã khám xong kỹ thuật (ServiceFinished) và chưa trả kết quả.");
        }

        // Server tự suy ra từ vé, KHÔNG nhận từ client
        var encounterId = ticket.EncounterId;
        var patientId = ticket.PatientId;
        var clsResultId = ticket.CLSResultId;

        // 3. Kiểm kiểu nội dung bằng magic bytes và kích thước tối đa 20 MB
        var (contentStream, verifiedContentType, sha256Hex, sizeBytes) =
            await FileValidationHelper.ValidateAndReadAsync(command.Content, command.FileName, command.ContentType, ct);

        // 4. Kiểm tra metadata nháp đã có trước đó (hỗ trợ gửi lại khi upload lỗi giữa chừng, không sinh bản ghi trùng)
        var existingDraft = await store.FindLatestDraftByTicketAndNameAsync(command.QueueTicketId, command.FileName, ct);
        ResultAttachment attachment;

        if (existingDraft is { UploadStatus: UploadStatus.Draft })
        {
            attachment = existingDraft;
            attachment.FileContentType = verifiedContentType;
            attachment.FileSizeBytes = sizeBytes;
            attachment.Sha256Hash = sha256Hex;
            await store.UpdateAsync(attachment, ct);
            await store.SaveChangesAsync(ct);
        }
        else
        {
            var cleanFileName = SanitizeFileName(command.FileName);
            var storageKey = $"results/{encounterId}/{clsResultId}/{Guid.NewGuid()}_{cleanFileName}";

            attachment = ResultAttachment.CreateDraft(
                Guid.NewGuid(),
                clsResultId,
                command.QueueTicketId,
                encounterId,
                patientId,
                storageKey,
                command.FileName,
                verifiedContentType,
                sizeBytes,
                sha256Hex,
                version: 1);

            await store.AddAsync(attachment, ct);
            await store.SaveChangesAsync(ct);
        }

        // 5. Tải lên Cloudflare R2 NGOÀI transaction DB
        try
        {
            await storage.UploadAsync(attachment.R2StorageKey, contentStream, verifiedContentType, ct);
        }
        catch (Exception ex)
        {
            // Lỗi upload: giữ metadata nháp, cho phép gửi lại, không sinh vé hay bản ghi mới
            throw new DocumentsException(DocumentsErrorCode.StorageError, $"Lỗi khi tải tệp lên kho lưu trữ R2: {ex.Message}");
        }

        // 6. Tải thành công -> đặt Uploaded và UploadedAtUtc
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        attachment.MarkUploaded(nowUtc);
        await store.UpdateAsync(attachment, ct);
        await store.SaveChangesAsync(ct);

        // 7. Ghi AuditLog
        var logPayload = JsonSerializer.Serialize(new
        {
            attachment.Id,
            attachment.QueueTicketId,
            attachment.CLSResultId,
            attachment.OriginalFileName,
            attachment.FileSizeBytes,
            attachment.Sha256Hash,
            attachment.UploadStatus
        });

        await auditLogPort.LogAsync(
            recordId: attachment.Id,
            actionType: "INSERT",
            changedColumns: "Id,CLSResultId,QueueTicketId,OriginalFileName,FileSizeBytes,Sha256Hash,UploadStatus,UploadedAtUtc",
            oldValuesJson: null,
            newValuesJson: logPayload,
            roomAccountId: command.RoomAccountId,
            deviceId: command.DeviceId,
            roomDeviceSessionId: command.RoomDeviceSessionId,
            changeReason: "Upload tệp kết quả CLS thành công",
            ct: ct);

        return ToDto(attachment);
    }

    public async Task MarkPublishedAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await store.FindByIdAsync(fileId, ct)
            ?? throw new DocumentsException(DocumentsErrorCode.FileNotFound, "Không tìm thấy tệp kết quả.");

        file.MarkPublished();
        await store.UpdateAsync(file, ct);
        await store.SaveChangesAsync(ct);
    }

    public async Task MarkClsResultFilesPublishedAsync(Guid clsResultId, CancellationToken ct = default)
    {
        var files = await store.GetByClsResultIdAsync(clsResultId, ct);
        foreach (var file in files)
        {
            if (file.UploadStatus != UploadStatus.Removed)
            {
                file.MarkPublished();
                await store.UpdateAsync(file, ct);
            }
        }

        await store.SaveChangesAsync(ct);
    }

    public async Task<DocumentFileStreamResult> OpenForDownloadAsync(Guid fileId, Guid requesterUserId, CancellationToken ct = default)
    {
        var file = await store.FindByIdAsync(fileId, ct);
        if (file == null || file.UploadStatus == UploadStatus.Removed)
        {
            throw new DocumentsException(DocumentsErrorCode.FileNotFound, "Không tìm thấy tệp kết quả hoặc tệp đã bị xóa.");
        }

        // 1. Từ chối tệp chưa IsPublished đối với mọi yêu cầu xem bệnh án thông thường
        if (!file.IsPublished)
        {
            throw new DocumentsException(DocumentsErrorCode.ResultNotPublished, "Tệp kết quả chưa được phát hành chính thức.");
        }

        // 2. Bác sĩ khám ban đầu xem được ngay khi Published
        var isDoctor = await clinicalModule.IsOriginalDoctorAsync(requesterUserId, file.EncounterId, ct);
        if (isDoctor)
        {
            var stream = await storage.DownloadAsync(file.R2StorageKey, ct);
            return new DocumentFileStreamResult(stream, file.FileContentType, file.OriginalFileName, file.FileSizeBytes);
        }

        // 3. Bệnh nhân / Người đại diện kiểm quyền truy cập hồ sơ (Verified-only)
        var canAccess = await identityPatientsModule.CanAccessPatientAsync(requesterUserId, file.PatientId, AccessLevel.ClinicalRecords, ct);
        if (!canAccess)
        {
            throw new DocumentsException(DocumentsErrorCode.AccessDenied, "Bạn không có quyền truy cập hồ sơ bệnh án này hoặc hồ sơ chưa được xác minh.");
        }

        // 4. Bệnh nhân / Người đại diện chỉ xem sau khi bác sĩ ban đầu phát hành kết luận sau CLS
        var isConcluded = await clinicalModule.IsPostClsConclusionPublishedAsync(file.EncounterId, ct);
        if (!isConcluded)
        {
            throw new DocumentsException(DocumentsErrorCode.DoctorNotConcluded, "Bác sĩ khám ban đầu chưa hoàn tất kết luận sau CLS. Tệp kết quả chưa thể hiển thị cho bệnh nhân.");
        }

        var patientStream = await storage.DownloadAsync(file.R2StorageKey, ct);
        return new DocumentFileStreamResult(patientStream, file.FileContentType, file.OriginalFileName, file.FileSizeBytes);
    }

    public async Task<IReadOnlyList<ResultAttachmentDto>> GetAttachmentsByQueueTicketIdAsync(Guid queueTicketId, Guid roomId, CancellationToken ct = default)
    {
        // Kiểm vé thuộc đúng phòng
        var ticket = await schedulingModule.ValidateClsTicketForRoomAsync(queueTicketId, roomId, ct);
        if (ticket == null || ticket.RoomId != roomId)
        {
            throw new DocumentsException(DocumentsErrorCode.TicketRoomMismatch, "Vé không thuộc quyền quản lý của phòng.");
        }

        var items = await store.GetByQueueTicketIdAsync(queueTicketId, ct);

        // Tài khoản phòng CHỈ thấy tệp Draft của chính phòng mình; sau khi Published không xem lại
        return items
            .Where(x => !x.IsPublished && x.UploadStatus != UploadStatus.Removed)
            .Select(ToAttachmentDto)
            .ToList();
    }

    public async Task<IReadOnlyList<ResultAttachmentDto>> GetPublishedAttachmentsByClsResultIdAsync(Guid clsResultId, CancellationToken ct = default)
    {
        var items = await store.GetByClsResultIdAsync(clsResultId, ct);
        return items
            .Where(x => x.IsPublished && x.UploadStatus != UploadStatus.Removed)
            .Select(ToAttachmentDto)
            .ToList();
    }

    public async Task<IReadOnlyList<ResultAttachmentDto>> GetPublishedAttachmentsByEncounterAsync(Guid encounterId, CancellationToken ct = default)
    {
        var items = await store.GetByEncounterIdAsync(encounterId, ct);
        return items
            .Where(x => x.IsPublished && x.UploadStatus != UploadStatus.Removed)
            .Select(ToAttachmentDto)
            .ToList();
    }

    public async Task<bool> DeleteDraftAttachmentAsync(Guid fileId, Guid roomId, CancellationToken ct = default)
    {
        var file = await store.FindByIdAsync(fileId, ct);
        if (file == null || file.UploadStatus == UploadStatus.Removed)
        {
            throw new DocumentsException(DocumentsErrorCode.FileNotFound, "Không tìm thấy tệp để xóa.");
        }

        if (file.IsPublished)
        {
            throw new DocumentsException(DocumentsErrorCode.InvalidOperation, "Không thể xóa tệp đã được phát hành chính thức.");
        }

        var ticket = await schedulingModule.ValidateClsTicketForRoomAsync(file.QueueTicketId, roomId, ct);
        if (ticket == null || ticket.RoomId != roomId)
        {
            throw new DocumentsException(DocumentsErrorCode.TicketRoomMismatch, "Tài khoản phòng chỉ được xóa tệp của chính phòng mình.");
        }

        file.MarkRemoved();
        await store.UpdateAsync(file, ct);
        await store.SaveChangesAsync(ct);

        // Xóa trên R2 (nếu có)
        try
        {
            await storage.DeleteAsync(file.R2StorageKey, ct);
        }
        catch
        {
            // Bỏ qua lỗi xóa vật lý trên R2, metadata đã đánh dấu Removed
        }

        await auditLogPort.LogAsync(
            recordId: file.Id,
            actionType: "DELETE",
            changedColumns: "UploadStatus",
            oldValuesJson: JsonSerializer.Serialize(new { file.UploadStatus }),
            newValuesJson: JsonSerializer.Serialize(new { UploadStatus = UploadStatus.Removed.ToString() }),
            roomAccountId: null,
            changeReason: "Phòng xóa tệp nháp trước khi trả kết quả",
            ct: ct);

        return true;
    }

    public async Task<StoredFileDto> AmendPublishedAttachmentAsync(Guid originalFileId, UploadResultFile command, string amendReason, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(amendReason);

        var original = await store.FindByIdAsync(originalFileId, ct)
            ?? throw new DocumentsException(DocumentsErrorCode.FileNotFound, "Không tìm thấy tệp gốc cần đính chính.");

        if (!original.IsPublished)
        {
            throw new DocumentsException(DocumentsErrorCode.InvalidOperation, "Chỉ được đính chính tệp đã phát hành.");
        }

        var (contentStream, verifiedContentType, sha256Hex, sizeBytes) =
            await FileValidationHelper.ValidateAndReadAsync(command.Content, command.FileName, command.ContentType, ct);

        var cleanFileName = SanitizeFileName(command.FileName);
        var storageKey = $"results/{original.EncounterId}/{original.CLSResultId}/{Guid.NewGuid()}_v{original.AttachmentVersion + 1}_{cleanFileName}";

        var correction = original.CreateCorrection(
            Guid.NewGuid(),
            storageKey,
            command.FileName,
            verifiedContentType,
            sizeBytes,
            sha256Hex);

        await store.AddAsync(correction, ct);
        await store.SaveChangesAsync(ct);

        // Tải lên R2 ngoài transaction DB
        await storage.UploadAsync(correction.R2StorageKey, contentStream, verifiedContentType, ct);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        correction.MarkUploaded(nowUtc);
        correction.MarkPublished(); // Phiên bản đính chính kế thừa trạng thái đã phát hành
        await store.UpdateAsync(correction, ct);
        await store.SaveChangesAsync(ct);

        await auditLogPort.LogAsync(
            recordId: correction.Id,
            actionType: "UPDATE",
            changedColumns: "AttachmentVersion,OriginalFileName,Sha256Hash,FileSizeBytes",
            oldValuesJson: JsonSerializer.Serialize(new { OriginalId = original.Id, original.AttachmentVersion }),
            newValuesJson: JsonSerializer.Serialize(new { CorrectionId = correction.Id, correction.AttachmentVersion, amendReason }),
            roomAccountId: command.RoomAccountId,
            deviceId: command.DeviceId,
            changeReason: amendReason,
            ct: ct);

        return ToDto(correction);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return Regex.Replace(name, @"[^a-zA-Z0-9_\.-]", "_");
    }

    private static StoredFileDto ToDto(ResultAttachment a) => new(
        a.Id,
        a.QueueTicketId,
        a.EncounterId,
        a.PatientId,
        a.CLSResultId,
        a.OriginalFileName,
        a.FileContentType,
        a.FileSizeBytes,
        a.Sha256Hash,
        a.UploadStatus.ToString(),
        a.IsPublished,
        a.UploadedAtUtc,
        a.AttachmentVersion);

    private static ResultAttachmentDto ToAttachmentDto(ResultAttachment a) => new(
        a.Id,
        a.CLSResultId,
        a.QueueTicketId,
        a.EncounterId,
        a.PatientId,
        a.OriginalFileName,
        a.FileContentType,
        a.FileSizeBytes,
        a.Sha256Hash,
        a.AttachmentVersion,
        a.UploadStatus.ToString(),
        a.IsPublished,
        a.UploadedAtUtc);
}
