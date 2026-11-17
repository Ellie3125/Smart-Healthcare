using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentsReporting.Application;
using DocumentsReporting.Contracts;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Shared.Abstractions;

namespace DocumentsReporting.Api;

public static class DocumentsEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ================= KÊNH NHÂN VIÊN: PHÒNG CLS =================
        var roomGroup = app.MapGroup("/api/documents/staff/room")
            .WithTags("DocumentsStaffRoom")
            .RequireAuthorization(Policies.RoomAccountOnly);

        // Upload tệp kết quả CLS theo vé (FR21, GP §6.4, §6.5)
        roomGroup.MapPost("/results/{queueTicketId:guid}/files", UploadResultFileAsync)
            .DisableAntiforgery();

        // Lấy danh sách tệp nháp của phòng theo vé
        roomGroup.MapGet("/results/{queueTicketId:guid}/files", GetRoomDraftFilesAsync);

        // Xóa tệp nháp của phòng trước khi phát hành
        roomGroup.MapDelete("/results/{queueTicketId:guid}/files/{fileId:guid}", DeleteRoomDraftFileAsync);

        // ================= KÊNH NHÂN VIÊN: BÁC SĨ KHÁM BAN ĐẦU =================
        var doctorGroup = app.MapGroup("/api/documents/staff/cls-results")
            .WithTags("DocumentsStaffDoctor")
            .RequireAuthorization(Policies.DoctorOnly);

        // Bác sĩ khám ban đầu tải/xem tệp kết quả sau khi phát hành
        doctorGroup.MapGet("/{resultId:guid}/files/{fileId:guid}/download", DownloadDoctorFileAsync);

        // ================= KÊNH BỆNH NHÂN: BỆNH NHÂN & NGƯỜI ĐẠI DIỆN =================
        var patientGroup = app.MapGroup("/api/documents/patient/cls-results")
            .WithTags("DocumentsPatient")
            .RequireAuthorization(Policies.PatientOnly);

        // Bệnh nhân/người đại diện tải/xem tệp kết quả (chỉ sau khi bác sĩ ban đầu kết luận sau CLS)
        patientGroup.MapGet("/{resultId:guid}/files/{fileId:guid}/download", DownloadPatientFileAsync);
    }

    private static async Task<IResult> UploadResultFileAsync(
        [FromRoute] Guid queueTicketId,
        [FromForm] IFormFile file,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-Device-Id")] string? deviceId,
        [FromHeader(Name = "X-Session-Id")] string? sessionId,
        ClaimsPrincipal user,
        [FromServices] IDocumentsModule documentsModule,
        [FromServices] IIdempotencyStore idempotencyStore,
        [FromServices] IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Tệp tải lên không hợp lệ",
                detail: "Vui lòng chọn tệp kết quả PDF, JPG hoặc PNG cần tải lên.");
        }

        // Lấy RoomId từ claim của RoomAccount
        var roomIdClaim = user.FindFirst(AppClaims.RoomId)?.Value;
        if (!Guid.TryParse(roomIdClaim, out var roomId) || roomId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Không xác định được phòng CLS",
                detail: "Tài khoản phòng không chứa thông tin RoomId hợp lệ.");
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdClaim, out var roomAccountId);

        Guid? sessionGuid = Guid.TryParse(sessionId, out var sGuid) ? sGuid : null;

        // Xử lý Idempotency nếu có header
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var rawHashInput = $"{queueTicketId}:{file.FileName}:{file.Length}";
            var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawHashInput)));

            var beginResult = await unitOfWork.ExecuteAsync(token => idempotencyStore.BeginAsync(
                callerScope: "DocumentsReporting",
                operationType: "UploadResultFile",
                key: idempotencyKey,
                requestHash: requestHash,
                ttl: TimeSpan.FromHours(1),
                operationId: null,
                ct: token), ct);

            if (beginResult is IdempotencyBeginResult.Conflict)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Xung đột khóa Idempotency",
                    detail: "Idempotency-Key đã được sử dụng với nội dung yêu cầu khác.");
            }

            if (beginResult is IdempotencyBeginResult.InProgress)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Yêu cầu đang được xử lý",
                    detail: "Một thao tác tải tệp cùng Idempotency-Key đang được thực hiện.");
            }

            if (beginResult is IdempotencyBeginResult.Replay replay)
            {
                if (!string.IsNullOrWhiteSpace(replay.BodyJson))
                {
                    var cachedDto = JsonSerializer.Deserialize<StoredFileDto>(replay.BodyJson);
                    return Results.Ok(cachedDto);
                }
            }
        }

        try
        {
            await using var fileStream = file.OpenReadStream();

            var command = new UploadResultFile(
                QueueTicketId: queueTicketId,
                RoomId: roomId,
                FileName: file.FileName,
                ContentType: file.ContentType,
                Content: fileStream,
                RoomAccountId: roomAccountId != Guid.Empty ? roomAccountId : null,
                DeviceId: deviceId,
                RoomDeviceSessionId: sessionGuid);

            var result = await documentsModule.UploadResultFileAsync(command, ct);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                await unitOfWork.ExecuteAsync(token => idempotencyStore.CompleteAsync(
                    callerScope: "DocumentsReporting",
                    operationType: "UploadResultFile",
                    key: idempotencyKey,
                    statusCode: StatusCodes.Status200OK,
                    bodyJson: JsonSerializer.Serialize(result),
                    operationId: null,
                    ct: token), ct);
            }

            return Results.Ok(result);
        }
        catch (DocumentsException dex)
        {
            return MapExceptionToProblem(dex);
        }
    }

    private static async Task<IResult> GetRoomDraftFilesAsync(
        [FromRoute] Guid queueTicketId,
        ClaimsPrincipal user,
        [FromServices] IDocumentsModule documentsModule,
        CancellationToken ct)
    {
        var roomIdClaim = user.FindFirst(AppClaims.RoomId)?.Value;
        if (!Guid.TryParse(roomIdClaim, out var roomId) || roomId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Không có quyền truy cập",
                detail: "Tài khoản phòng không hợp lệ.");
        }

        try
        {
            var files = await documentsModule.GetAttachmentsByQueueTicketIdAsync(queueTicketId, roomId, ct);
            return Results.Ok(files);
        }
        catch (DocumentsException dex)
        {
            return MapExceptionToProblem(dex);
        }
    }

    private static async Task<IResult> DeleteRoomDraftFileAsync(
        [FromRoute] Guid queueTicketId,
        [FromRoute] Guid fileId,
        ClaimsPrincipal user,
        [FromServices] IDocumentsModule documentsModule,
        CancellationToken ct)
    {
        var roomIdClaim = user.FindFirst(AppClaims.RoomId)?.Value;
        if (!Guid.TryParse(roomIdClaim, out var roomId) || roomId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Không có quyền truy cập",
                detail: "Tài khoản phòng không hợp lệ.");
        }

        try
        {
            var success = await documentsModule.DeleteDraftAttachmentAsync(fileId, roomId, ct);
            return Results.Ok(new { success });
        }
        catch (DocumentsException dex)
        {
            return MapExceptionToProblem(dex);
        }
    }

    private static async Task<IResult> DownloadDoctorFileAsync(
        [FromRoute] Guid resultId,
        [FromRoute] Guid fileId,
        ClaimsPrincipal user,
        [FromServices] IDocumentsModule documentsModule,
        CancellationToken ct)
    {
        var doctorIdClaim = user.FindFirst(AppClaims.DoctorId)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(doctorIdClaim, out var doctorId) || doctorId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Không xác định danh tính bác sĩ",
                detail: "Token không chứa thông tin định danh bác sĩ.");
        }

        try
        {
            var streamResult = await documentsModule.OpenForDownloadAsync(fileId, doctorId, ct);
            return Results.File(streamResult.Content, streamResult.ContentType, streamResult.FileName);
        }
        catch (DocumentsException dex)
        {
            return MapExceptionToProblem(dex);
        }
    }

    private static async Task<IResult> DownloadPatientFileAsync(
        [FromRoute] Guid resultId,
        [FromRoute] Guid fileId,
        ClaimsPrincipal user,
        [FromServices] IDocumentsModule documentsModule,
        CancellationToken ct)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Chưa đăng nhập",
                detail: "Yêu cầu đăng nhập tài khoản bệnh nhân để xem kết quả.");
        }

        try
        {
            var streamResult = await documentsModule.OpenForDownloadAsync(fileId, userId, ct);
            return Results.File(streamResult.Content, streamResult.ContentType, streamResult.FileName);
        }
        catch (DocumentsException dex)
        {
            return MapExceptionToProblem(dex);
        }
    }

    private static IResult MapExceptionToProblem(DocumentsException dex)
    {
        var (statusCode, title) = dex.ErrorCode switch
        {
            DocumentsErrorCode.InvalidFileType => (StatusCodes.Status415UnsupportedMediaType, "Định dạng tệp không được hỗ trợ"),
            DocumentsErrorCode.FileTooLarge => (StatusCodes.Status413PayloadTooLarge, "Dung lượng tệp vượt quá giới hạn"),
            DocumentsErrorCode.TicketNotFound => (StatusCodes.Status404NotFound, "Không tìm thấy vé"),
            DocumentsErrorCode.FileNotFound => (StatusCodes.Status404NotFound, "Không tìm thấy tệp kết quả"),
            DocumentsErrorCode.TicketRoomMismatch => (StatusCodes.Status403Forbidden, "Vé không thuộc quyền phòng này"),
            DocumentsErrorCode.AccessDenied => (StatusCodes.Status403Forbidden, "Không có quyền truy cập hồ sơ bệnh nhân"),
            DocumentsErrorCode.ResultNotPublished => (StatusCodes.Status403Forbidden, "Kết quả chưa được phát hành"),
            DocumentsErrorCode.DoctorNotConcluded => (StatusCodes.Status403Forbidden, "Bác sĩ chưa hoàn tất kết luận sau CLS"),
            DocumentsErrorCode.RoomCannotViewPublished => (StatusCodes.Status403Forbidden, "Phòng không được xem lại kết quả đã phát hành"),
            DocumentsErrorCode.InvalidTicketStatus => (StatusCodes.Status409Conflict, "Trạng thái vé không hợp lệ"),
            DocumentsErrorCode.InvalidOperation => (StatusCodes.Status409Conflict, "Thao tác không hợp lệ"),
            DocumentsErrorCode.StorageError => (StatusCodes.Status500InternalServerError, "Lỗi lưu trữ đối tượng"),
            _ => (StatusCodes.Status400BadRequest, "Yêu cầu không hợp lệ")
        };

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: dex.Message);
    }
}
