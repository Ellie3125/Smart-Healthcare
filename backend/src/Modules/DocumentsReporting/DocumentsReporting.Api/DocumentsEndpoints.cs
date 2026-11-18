using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Billing.Contracts;
using Clinical.Contracts;
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
        var patientClsGroup = app.MapGroup("/api/documents/patient/cls-results")
            .WithTags("DocumentsPatient")
            .RequireAuthorization(Policies.PatientOnly);

        // Bệnh nhân/người đại diện tải/xem tệp kết quả (chỉ sau khi bác sĩ ban đầu kết luận sau CLS)
        patientClsGroup.MapGet("/{resultId:guid}/files/{fileId:guid}/download", DownloadPatientFileAsync);

        var patientEncountersGroup = app.MapGroup("/api/documents/patient/encounters")
            .WithTags("DocumentsPatient")
            .RequireAuthorization(Policies.PatientOnly);

        // Danh sách tóm tắt lịch sử khám của bệnh nhân (phân trang, chỉ hồ sơ Verified)
        patientEncountersGroup.MapGet("", GetPatientEncountersListAsync)
            .WithName("GetPatientEncountersList")
            .WithSummary("Danh sách tóm tắt lịch sử khám của bệnh nhân (phân trang, chỉ hồ sơ Verified)")
            .Produces<PatientEncountersResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);

        // Chi tiết bệnh án Published; kết quả CLS chỉ sau kết luận sau CLS
        patientEncountersGroup.MapGet("/{id:guid}", GetPatientEncounterDetailAsync)
            .WithName("GetPatientEncounterDetail")
            .WithSummary("Chi tiết bệnh án Published; kết quả CLS chỉ sau kết luận sau CLS")
            .Produces<PatientEncounterDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized);
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

    private static async Task<IResult> GetPatientEncountersListAsync(
        [FromQuery] Guid? patientProfileId,
        [FromQuery] Guid? patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        ClaimsPrincipal user = null!,
        [FromServices] IDocumentsModule documentsModule = null!,
        [FromServices] IClinicalModule clinicalModule = null!,
        [FromServices] IIdentityPatientsModule identityModule = null!,
        CancellationToken ct = default)
    {
        var targetPatientId = patientProfileId ?? patientId;
        if (!targetPatientId.HasValue || targetPatientId.Value == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Yêu cầu không hợp lệ",
                detail: "patientProfileId là bắt buộc.");
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Chưa đăng nhập",
                detail: "Yêu cầu đăng nhập tài khoản bệnh nhân để xem lịch sử khám.");
        }

        // Kiểm quyền trên hồ sơ (chỉ Verified mới có quyền ClinicalRecords - SEC-01 & GP §8.1.6)
        var canAccess = await identityModule.CanAccessPatientAsync(userId, targetPatientId.Value, AccessLevel.ClinicalRecords, ct);
        if (!canAccess)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "AccessDenied",
                detail: "Hồ sơ chưa được xác minh danh tính (Verified) hoặc bạn không có quyền xem bệnh án.");
        }

        if (page < 1) page = 1;
        if (pageSize is < 1 or > 50) pageSize = 10;

        var encounters = await clinicalModule.GetPatientEncountersAsync(targetPatientId.Value, ct);
        var totalCount = encounters.Count;
        var paged = encounters.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var summaryList = new List<PatientEncounterSummaryDto>(paged.Count);
        foreach (var enc in paged)
        {
            var fullEnc = await clinicalModule.GetEncounterByIdAsync(enc.Id, ct);
            var doctorName = "Bác sĩ phụ trách";
            if (fullEnc is not null)
            {
                var doctor = await identityModule.GetDoctorAsync(fullEnc.OriginalDoctorId, ct);
                if (doctor is not null)
                {
                    doctorName = doctor.FullName;
                }
            }
            else if (!string.IsNullOrWhiteSpace(enc.DoctorRankSnapshot))
            {
                doctorName = $"{enc.DoctorRankSnapshot} ({enc.SpecialtySnapshot})";
            }

            var clinicalRecord = await clinicalModule.GetClinicalRecordByEncounterIdAsync(enc.Id, ct);
            var diagnoses = await clinicalModule.GetPublishedDiagnosesAsync(enc.Id, ct);
            var primaryDiagnosis = (diagnoses.FirstOrDefault(d => d.IsPrimary)
                ?? diagnoses.FirstOrDefault(d => d.DiagnosisType == "Final") ?? diagnoses.FirstOrDefault())?.DiagnosisName;
            var orders = await clinicalModule.GetServiceOrdersByEncounterIdAsync(enc.Id, ct);
            var hasClsResults = orders.SelectMany(o => o.Items).Any(i => i.Result is { ResultStatus: "Published" });

            var prescriptions = await clinicalModule.GetPrescriptionsByEncounterIdAsync(enc.Id, ct);
            var hasPrescription = prescriptions.Count > 0;

            var attachments = await documentsModule.GetPublishedAttachmentsByEncounterAsync(enc.Id, ct);
            var hasAttachments = attachments.Count > 0;

            var isPublished = clinicalRecord is { Status: "Published" };
            var code = $"PK-{enc.Id.ToString()[..8].ToUpperInvariant()}";

            summaryList.Add(new PatientEncounterSummaryDto(
                Id: enc.Id,
                EncounterCode: code,
                ServiceDate: enc.CheckedInAtUtc,
                DoctorName: doctorName,
                SpecialtyName: enc.SpecialtySnapshot,
                PrimaryDiagnosis: primaryDiagnosis,
                Status: enc.Status,
                HasPrescription: hasPrescription,
                HasPublishedAttachments: hasAttachments,
                PatientName: null,
                PatientProfileId: targetPatientId.Value,
                IsPublished: isPublished,
                HasClsResults: hasClsResults));
        }

        var response = new PatientEncountersResponse(
            summaryList,
            totalCount,
            page,
            pageSize,
            (page * pageSize) < totalCount);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetPatientEncounterDetailAsync(
        [FromRoute] Guid id,
        ClaimsPrincipal user,
        [FromServices] IDocumentsModule documentsModule,
        [FromServices] IClinicalModule clinicalModule,
        [FromServices] IBillingModule billingModule,
        [FromServices] IIdentityPatientsModule identityModule,
        CancellationToken ct)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Chưa đăng nhập",
                detail: "Yêu cầu đăng nhập tài khoản bệnh nhân để xem chi tiết lượt khám.");
        }

        var encounter = await clinicalModule.GetEncounterByIdAsync(id, ct);
        if (encounter is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "EncounterNotFound",
                detail: "Không tìm thấy thông tin lượt khám.");
        }

        // Kiểm quyền trên hồ sơ bệnh nhân (AccessLevel.ClinicalRecords)
        var canAccess = await identityModule.CanAccessPatientAsync(userId, encounter.PatientId, AccessLevel.ClinicalRecords, ct);
        if (!canAccess)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "AccessDenied",
                detail: "Bạn không có quyền truy cập hồ sơ bệnh án này hoặc hồ sơ chưa được xác minh.");
        }

        var clinicalRecord = await clinicalModule.GetClinicalRecordByEncounterIdAsync(id, ct);
        if (clinicalRecord is not { Status: "Published" })
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "EncounterNotFound",
                detail: "Bệnh án chưa được phát hành.");
        var diagnoses = await clinicalModule.GetPublishedDiagnosesAsync(id, ct);

        var doctor = await identityModule.GetDoctorAsync(encounter.OriginalDoctorId, ct);
        var doctorName = doctor?.FullName ?? "Bác sĩ phụ trách";
        var room = await identityModule.GetRoomAsync(encounter.OriginalRoomId, ct);
        var roomName = room?.Name;

        var prescriptions = await clinicalModule.GetPrescriptionsByEncounterIdAsync(id, ct);
        var hasPrescription = prescriptions.Count > 0;

        // Kiểm tra bác sĩ khám ban đầu đã phát hành kết luận sau CLS chưa (GP §6.5)
        var isPostClsPublished = await clinicalModule.IsPostClsConclusionPublishedAsync(id, ct);

        // Lấy 5 chỉ tiêu chi phí từ Billing (không tự tính lại)
        EncounterBillingSummaryDto? billingSummary = null;
        try
        {
            var cost = await billingModule.GetEncounterCostSummaryAsync(id, ct);
            if (cost is not null)
            {
                billingSummary = new EncounterBillingSummaryDto(
                    cost.EncounterId,
                    "VND",
                    cost.TotalPayable,
                    cost.TotalPaid,
                    cost.TotalRefunded,
                    cost.NetCollected,
                    cost.Outstanding,
                    cost.CreditPendingRefund);
            }
        }
        catch
        {
            // Nếu chưa có giao dịch hoặc billing chưa phát sinh khoản thu cho lượt này
        }

        var code = $"PK-{encounter.Id.ToString()[..8].ToUpperInvariant()}";

        // Kết quả và tệp CLS chỉ mở sau kết luận sau CLS; chẩn đoán Published không phụ thuộc điều kiện này.
        var serviceOrders = isPostClsPublished ? await clinicalModule.GetServiceOrdersByEncounterIdAsync(id, ct) : [];
        var publishedAttachments = isPostClsPublished ? await documentsModule.GetPublishedAttachmentsByEncounterAsync(id, ct) : [];

        var clsResultsList = new List<EncounterClsResultDto>();
        foreach (var order in serviceOrders)
        {
            foreach (var item in order.Items.Where(i => i.Result is { ResultStatus: "Published" }))
            {
                var itemAttachments = item.Result is null
                    ? []
                    : publishedAttachments.Where(a => a.CLSResultId == item.Result.Id).ToList();

                clsResultsList.Add(new EncounterClsResultDto(
                    Id: item.Result?.Id ?? item.Id,
                    ServiceCode: item.Id.ToString()[..8],
                    ServiceName: item.ServiceNameSnapshot,
                    Status: item.Result?.ResultStatus ?? item.ItemStatus,
                    SummaryText: item.Result?.ResultSummary,
                    TechnicalConclusion: item.Result?.DetailedFindings,
                    Attachments: itemAttachments));
            }
        }

        var detail = new PatientEncounterDetailDto(
            Id: encounter.Id,
            EncounterCode: code,
            ServiceDate: encounter.CheckedInAtUtc,
            DoctorName: doctorName,
            SpecialtyName: encounter.SpecialtySnapshot,
            DoctorRankName: encounter.DoctorRankSnapshot,
            RoomName: roomName,
            Symptoms: clinicalRecord?.Symptoms ?? clinicalRecord?.ChiefComplaint,
            ClinicalNotes: isPostClsPublished ? clinicalRecord?.ClinicalExaminationNotes : null,
            Vitals: null,
            Diagnoses: diagnoses.Select(d => new DocumentsReporting.Contracts.EncounterDiagnosisDto(
                d.ICD10Code, d.DiagnosisName, d.IsPrimary, d.DiagnosisType)).ToArray(),
            Conclusion: isPostClsPublished ? clinicalRecord?.PostCLSConclusion : null,
            FollowUpInstruction: isPostClsPublished ? clinicalRecord?.FollowUpInstructions : null,
            PublishedAtUtc: clinicalRecord?.PublishedAtUtc,
            Version: 1,
            IsPublished: true,
            HasPrescription: hasPrescription,
            ClsResults: clsResultsList,
            BillingSummary: billingSummary,
            PatientName: null);

        return Results.Ok(detail);
    }
}

public sealed record PatientEncountersResponse(
    IReadOnlyList<PatientEncounterSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasMore,
    bool BlockedPendingProfile = false);
