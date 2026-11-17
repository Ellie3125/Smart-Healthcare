using System.Security.Claims;
using Clinical.Contracts;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Clinical.Api;

internal static class ClinicalEndpoints
{
    public static RouteGroupBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var root = app.MapGroup("/api/clinical").WithTags("Clinical");

        // ─── Kênh staff: nhân viên theo vai trò ───────────────────────────────
        var nurse  = root.MapGroup("/staff/nurse").RequireAuthorization(Policies.NurseOnly);
        var doctor = root.MapGroup("/staff/doctor").RequireAuthorization(Policies.DoctorOnly);
        var receptionist = root.MapGroup("/staff/receptionist").RequireAuthorization(Policies.ReceptionistOnly);
        var room   = root.MapGroup("/staff/room").RequireAuthorization(Policies.RoomAccountOnly);

        // ─── Kênh bệnh nhân ───────────────────────────────────────────────────
        var patient = root.MapGroup("/patient").RequireAuthorization(Policies.PatientOnly);

        // ═══════════════════════════════════════════════════════════════════════
        // POST /encounters/check-in — Tiếp nhận bệnh nhân (Receptionist + Nurse)
        // TODO: Chờ trưởng nhóm chốt: Encounter do Scheduling cấp Id hay Clinical tự tạo.
        //       Route này CHƯA được map. Handler StartEncounterAsync vẫn còn trong ClinicalModule.
        // ═══════════════════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════════════════════════
        // GET /encounters/{id} — Lấy chi tiết lượt khám
        //   Staff (nurse, doctor): xem bất kỳ lượt khám
        //   Patient: chỉ xem lượt khám thuộc hồ sơ bệnh nhân của mình
        // ═══════════════════════════════════════════════════════════════════════
        nurse.MapGet("/encounters/{id:guid}", GetEncounterByIdAsync)
            .WithName("GetEncounterById_Nurse")
            .WithSummary("Lấy chi tiết lượt khám theo ID (Điều dưỡng)")
            .Produces<EncounterDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        doctor.MapGet("/encounters/{id:guid}", GetEncounterByIdAsync)
            .WithName("GetEncounterById_Doctor")
            .WithSummary("Lấy chi tiết lượt khám theo ID (Bác sĩ)")
            .Produces<EncounterDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        patient.MapGet("/encounters/{id:guid}", GetEncounterByIdPatientAsync)
            .WithName("GetEncounterById_Patient")
            .WithSummary("Lấy chi tiết lượt khám theo ID (Bệnh nhân — chỉ xem hồ sơ của mình)")
            .Produces<EncounterDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // ═══════════════════════════════════════════════════════════════════════
        // GET /encounters/active-by-patient/{patientId} — Kiểm tra lượt khám đang hoạt động
        //   Staff only (Receptionist + Nurse): để tiếp tân/điều dưỡng kiểm trước khi check-in
        // ═══════════════════════════════════════════════════════════════════════
        receptionist.MapGet("/encounters/active-by-patient/{patientId:guid}", CheckActiveEncounterAsync)
            .WithName("CheckActiveEncounter_Receptionist")
            .WithSummary("Kiểm tra lượt khám đang hoạt động của bệnh nhân (Tiếp tân)")
            .Produces<ActiveEncounterCheckResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        nurse.MapGet("/encounters/active-by-patient/{patientId:guid}", CheckActiveEncounterAsync)
            .WithName("CheckActiveEncounter_Nurse")
            .WithSummary("Kiểm tra lượt khám đang hoạt động của bệnh nhân (Điều dưỡng)")
            .Produces<ActiveEncounterCheckResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // ═══════════════════════════════════════════════════════════════════════
        // GET /encounters/patient/{patientId} — Lịch sử lượt khám của bệnh nhân
        //   Staff (nurse, doctor): xem bất kỳ
        //   Patient: chỉ xem lịch sử của hồ sơ mình có quyền
        // ═══════════════════════════════════════════════════════════════════════
        nurse.MapGet("/encounters/patient/{patientId:guid}", GetPatientEncountersAsync)
            .WithName("GetPatientEncounters_Nurse")
            .WithSummary("Lấy lịch sử lượt khám của bệnh nhân (Điều dưỡng)")
            .Produces<IReadOnlyList<EncounterSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        doctor.MapGet("/encounters/patient/{patientId:guid}", GetPatientEncountersAsync)
            .WithName("GetPatientEncounters_Doctor")
            .WithSummary("Lấy lịch sử lượt khám của bệnh nhân (Bác sĩ)")
            .Produces<IReadOnlyList<EncounterSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        patient.MapGet("/encounters/patient/{patientId:guid}", GetPatientEncountersPatientAsync)
            .WithName("GetPatientEncounters_Patient")
            .WithSummary("Lấy lịch sử lượt khám (Bệnh nhân — chỉ xem hồ sơ của mình)")
            .Produces<IReadOnlyList<EncounterSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // ═══════════════════════════════════════════════════════════════════════
        // GET /encounters/{id}/record — Hồ sơ bệnh án lâm sàng
        //   Staff (nurse, doctor); Patient của đúng lượt khám đó
        // ═══════════════════════════════════════════════════════════════════════
        nurse.MapGet("/encounters/{id:guid}/record", GetClinicalRecordAsync)
            .WithName("GetClinicalRecord_Nurse")
            .WithSummary("Lấy hồ sơ bệnh án lâm sàng (Điều dưỡng)")
            .Produces<EncounterClinicalRecordDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        doctor.MapGet("/encounters/{id:guid}/record", GetClinicalRecordAsync)
            .WithName("GetClinicalRecord_Doctor")
            .WithSummary("Lấy hồ sơ bệnh án lâm sàng (Bác sĩ)")
            .Produces<EncounterClinicalRecordDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        patient.MapGet("/encounters/{id:guid}/record", GetClinicalRecordPatientAsync)
            .WithName("GetClinicalRecord_Patient")
            .WithSummary("Lấy hồ sơ bệnh án lâm sàng (Bệnh nhân — chỉ xem hồ sơ của mình)")
            .Produces<EncounterClinicalRecordDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // ═══════════════════════════════════════════════════════════════════════
        // GET /encounters/{id}/service-orders — Phiếu chỉ định CLS
        //   Staff: nurse, doctor, room-account (phòng CLS); Patient: chỉ hồ sơ của mình
        // ═══════════════════════════════════════════════════════════════════════
        nurse.MapGet("/encounters/{id:guid}/service-orders", GetServiceOrdersAsync)
            .WithName("GetServiceOrders_Nurse")
            .WithSummary("Lấy phiếu chỉ định CLS (Điều dưỡng)")
            .Produces<IReadOnlyList<ServiceOrderDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        doctor.MapGet("/encounters/{id:guid}/service-orders", GetServiceOrdersAsync)
            .WithName("GetServiceOrders_Doctor")
            .WithSummary("Lấy phiếu chỉ định CLS (Bác sĩ)")
            .Produces<IReadOnlyList<ServiceOrderDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        room.MapGet("/encounters/{id:guid}/service-orders", GetServiceOrdersAsync)
            .WithName("GetServiceOrders_Room")
            .WithSummary("Lấy phiếu chỉ định CLS (Tài khoản phòng CLS)")
            .Produces<IReadOnlyList<ServiceOrderDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        patient.MapGet("/encounters/{id:guid}/service-orders", GetServiceOrdersPatientAsync)
            .WithName("GetServiceOrders_Patient")
            .WithSummary("Lấy phiếu chỉ định CLS (Bệnh nhân — chỉ xem hồ sơ của mình)")
            .Produces<IReadOnlyList<ServiceOrderDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // ═══════════════════════════════════════════════════════════════════════
        // GET /encounters/{id}/prescriptions — Đơn thuốc
        //   Staff (nurse, doctor); Patient của đúng lượt khám đó
        // ═══════════════════════════════════════════════════════════════════════
        nurse.MapGet("/encounters/{id:guid}/prescriptions", GetPrescriptionsAsync)
            .WithName("GetPrescriptions_Nurse")
            .WithSummary("Lấy danh sách đơn thuốc (Điều dưỡng)")
            .Produces<IReadOnlyList<PrescriptionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        doctor.MapGet("/encounters/{id:guid}/prescriptions", GetPrescriptionsAsync)
            .WithName("GetPrescriptions_Doctor")
            .WithSummary("Lấy danh sách đơn thuốc (Bác sĩ)")
            .Produces<IReadOnlyList<PrescriptionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        patient.MapGet("/encounters/{id:guid}/prescriptions", GetPrescriptionsPatientAsync)
            .WithName("GetPrescriptions_Patient")
            .WithSummary("Lấy danh sách đơn thuốc (Bệnh nhân — chỉ xem hồ sơ của mình)")
            .Produces<IReadOnlyList<PrescriptionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return root;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Handlers — Staff channel (không cần kiểm quyền theo hồ sơ cụ thể)
    // ═══════════════════════════════════════════════════════════════════════════

    private static async Task<IResult> GetEncounterByIdAsync(
        Guid id,
        IClinicalModule clinical,
        CancellationToken ct)
    {
        try
        {
            var encounter = await clinical.GetEncounterByIdAsync(id, ct);
            return encounter is null
                ? ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", $"Không tìm thấy lượt khám với ID {id}.")
                : TypedResults.Ok(encounter);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> CheckActiveEncounterAsync(
        Guid patientId,
        IClinicalModule clinical,
        CancellationToken ct)
    {
        try
        {
            var hasActive = await clinical.HasActiveEncounterAsync(patientId, ct);
            return TypedResults.Ok(new ActiveEncounterCheckResponse(patientId, hasActive));
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> GetPatientEncountersAsync(
        Guid patientId,
        IClinicalModule clinical,
        CancellationToken ct)
    {
        try
        {
            var history = await clinical.GetPatientEncountersAsync(patientId, ct);
            return TypedResults.Ok(history);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> GetClinicalRecordAsync(
        Guid id,
        IClinicalModule clinical,
        CancellationToken ct)
    {
        try
        {
            var record = await clinical.GetClinicalRecordByEncounterIdAsync(id, ct);
            return record is null
                ? ClinicalProblem(StatusCodes.Status404NotFound, "ClinicalRecordNotFound", $"Không tìm thấy hồ sơ bệnh án cho lượt khám {id}.")
                : TypedResults.Ok(record);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> GetServiceOrdersAsync(
        Guid id,
        IClinicalModule clinical,
        CancellationToken ct)
    {
        try
        {
            var orders = await clinical.GetServiceOrdersByEncounterIdAsync(id, ct);
            return TypedResults.Ok(orders);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> GetPrescriptionsAsync(
        Guid id,
        IClinicalModule clinical,
        CancellationToken ct)
    {
        try
        {
            var prescriptions = await clinical.GetPrescriptionsByEncounterIdAsync(id, ct);
            return TypedResults.Ok(prescriptions);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Handlers — Patient channel (phải kiểm quyền trên đúng hồ sơ bệnh nhân)
    // Lấy userId từ token; KHÔNG nhận từ body/query.
    // Dùng IIdentityPatientsModule.CanAccessPatientAsync để kiểm quyền theo hồ sơ.
    // ═══════════════════════════════════════════════════════════════════════════

    private static async Task<IResult> GetEncounterByIdPatientAsync(
        Guid id,
        ClaimsPrincipal user,
        IClinicalModule clinical,
        IIdentityPatientsModule identity,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        try
        {
            var encounter = await clinical.GetEncounterByIdAsync(id, ct);
            if (encounter is null)
                return ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy lượt khám.");

            // Kiểm quyền: người gọi phải là bệnh nhân hoặc người đại diện của PatientId trong lượt khám này.
            if (!await identity.CanAccessPatientAsync(userId, encounter.PatientId, AccessLevel.ClinicalRecords, ct))
                return ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy lượt khám.");

            return TypedResults.Ok(encounter);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> GetPatientEncountersPatientAsync(
        Guid patientId,
        ClaimsPrincipal user,
        IClinicalModule clinical,
        IIdentityPatientsModule identity,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        // Kiểm quyền: người gọi phải là bệnh nhân hoặc người đại diện của patientId.
        if (!await identity.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords, ct))
            return ClinicalProblem(StatusCodes.Status404NotFound, "PatientNotFound", "Không tìm thấy hồ sơ bệnh nhân.");

        try
        {
            var history = await clinical.GetPatientEncountersAsync(patientId, ct);
            return TypedResults.Ok(history);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> GetClinicalRecordPatientAsync(
        Guid id,
        ClaimsPrincipal user,
        IClinicalModule clinical,
        IIdentityPatientsModule identity,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        try
        {
            var encounter = await clinical.GetEncounterByIdAsync(id, ct);
            if (encounter is null)
                return ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy lượt khám.");

            if (!await identity.CanAccessPatientAsync(userId, encounter.PatientId, AccessLevel.ClinicalRecords, ct))
                return ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy lượt khám.");

            var record = await clinical.GetClinicalRecordByEncounterIdAsync(id, ct);
            return record is null
                ? ClinicalProblem(StatusCodes.Status404NotFound, "ClinicalRecordNotFound", "Không tìm thấy hồ sơ bệnh án.")
                : TypedResults.Ok(record);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> GetServiceOrdersPatientAsync(
        Guid id,
        ClaimsPrincipal user,
        IClinicalModule clinical,
        IIdentityPatientsModule identity,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        try
        {
            var encounter = await clinical.GetEncounterByIdAsync(id, ct);
            if (encounter is null)
                return ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy lượt khám.");

            if (!await identity.CanAccessPatientAsync(userId, encounter.PatientId, AccessLevel.ClinicalRecords, ct))
                return ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy lượt khám.");

            var orders = await clinical.GetServiceOrdersByEncounterIdAsync(id, ct);
            return TypedResults.Ok(orders);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    private static async Task<IResult> GetPrescriptionsPatientAsync(
        Guid id,
        ClaimsPrincipal user,
        IClinicalModule clinical,
        IIdentityPatientsModule identity,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        try
        {
            var encounter = await clinical.GetEncounterByIdAsync(id, ct);
            if (encounter is null)
                return ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy lượt khám.");

            if (!await identity.CanAccessPatientAsync(userId, encounter.PatientId, AccessLevel.ClinicalRecords, ct))
                return ClinicalProblem(StatusCodes.Status404NotFound, "EncounterNotFound", "Không tìm thấy lượt khám.");

            var prescriptions = await clinical.GetPrescriptionsByEncounterIdAsync(id, ct);
            return TypedResults.Ok(prescriptions);
        }
        catch (ClinicalException ex)
        {
            return MapClinicalException(ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Lấy userId từ JWT claim (sub hoặc NameIdentifier). Không được nhận từ body/query.</summary>
    private static bool TryGetUserId(ClaimsPrincipal user, out Guid id) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id)
        && id != Guid.Empty;

    /// <summary>Ánh xạ ClinicalException sang ProblemDetails theo conventions.md §4 (trường lỗi là `code`).</summary>
    private static IResult MapClinicalException(ClinicalException ex) => ex.Code switch
    {
        ClinicalErrorCode.InvalidRequest =>
            ClinicalProblem(StatusCodes.Status400BadRequest, "InvalidInput", ex.Message),
        ClinicalErrorCode.ActiveEncounterAlreadyExists =>
            ClinicalProblem(StatusCodes.Status409Conflict, ex.Code.ToString(), ex.Message),
        ClinicalErrorCode.EncounterNotFound =>
            ClinicalProblem(StatusCodes.Status404NotFound, ex.Code.ToString(), ex.Message),
        _ => ClinicalProblem(StatusCodes.Status400BadRequest, ex.Code.ToString(), ex.Message)
    };

    /// <summary>Tạo ProblemDetails theo RFC 9457 với extension `code` (conventions.md §4).</summary>
    private static IResult ClinicalProblem(int statusCode, string code, string title) =>
        TypedResults.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

public sealed record ActiveEncounterCheckResponse(Guid PatientId, bool HasActiveEncounter);
