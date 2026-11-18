using System.Globalization;
using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.Appointments;
using Scheduling.Contracts;
using Scheduling.Domain.Enums;
using Shared.Abstractions;

namespace Scheduling.Api;

internal static class PatientAppointmentEndpoints
{
    private const int MaxPage = 1000;
    private const int MaxPageSize = 50;

    public static void MapPatientAppointmentEndpoints(this RouteGroupBuilder scheduling)
    {
        var patient = scheduling.MapGroup("/patient/appointments").RequireAuthorization(Policies.PatientOnly);
        patient.MapGet("", ListAsync)
            .WithSummary("Lịch hẹn người gọi đã đặt, mới nhất trước (Patient)")
            .Produces<AppointmentPage>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        patient.MapGet("/{appointmentId:guid}", GetAsync)
            .WithSummary("Chi tiết lịch hẹn và mã lịch hẹn (Patient)")
            .Produces<AppointmentItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        patient.MapPost("/{appointmentId:guid}/cancel", CancelAsync)
            .WithSummary("Bệnh nhân hủy lịch Scheduled; hoàn phí khi trước 05:00 ngày khám (Patient)")
            .Produces<CancelResponse>().Produces<OperationAccepted>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        patient.MapGet("/{appointmentId:guid}/cancellation-preview", PreviewCancelAsync)
            .WithSummary("Xem trước số tiền được hoàn nếu hủy ngay bây giờ (Patient)")
            .Produces<CancellationPreview>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        patient.MapPost("/{appointmentId:guid}/reschedule", RescheduleAsync)
            .WithSummary("Đổi lịch sang bác sĩ/ngày cùng mức phí trước 05:00 ngày khám cũ (Patient)")
            .Produces<AppointmentItem>().Produces<OperationAccepted>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
    }

    private static async Task<IResult> CancelAsync(Guid appointmentId, ClaimsPrincipal user, HttpRequest request,
        [FromServices] IPatientAppointmentReader reader, [FromServices] IIdentityPatientsModule identity,
        [FromServices] AppointmentChangeService changes, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();
        var (key, body, error) = await ReadAsync<CancelBody>(request, allowEmptyBody: true, ct);
        if (error is not null)
            return error;
        var row = await OwnedAsync(appointmentId, userId, reader, identity, ct);
        if (row is null)
            return NotFound();

        var outcome = await changes.CancelAsync(new CancelRequest(appointmentId, userId, CancellationInitiator.Patient,
            row.PatientId, body?.Reason, null, key!), ct);
        return outcome switch
        {
            { Result: ChangeResult.Cancelled, Cancelled: { } c } => TypedResults.Ok(new CancelResponse(appointmentId,
                "Cancelled", c.SlotReleased ? "Full" : "None", c.SlotReleased ? row.AppliedConsultationFee : 0m, "VND",
                c.CancelledAtUtc, outcome.OperationId)),
            _ => ChangeProblem(outcome),
        };
    }

    // Tiếp tân hủy tại quầy (Phạm vi §6.2): Scheduled theo cùng chính sách hoàn; CheckedIn bắt buộc lý do, chỉ khi bệnh nhân chưa vào khám.
    public static void MapStaffCancelEndpoint(this RouteGroupBuilder scheduling) =>
        scheduling.MapGroup("/staff/appointments").RequireAuthorization(Policies.ReceptionistOnly)
            .MapPost("/{appointmentId:guid}/cancel", StaffCancelAsync)
            .WithSummary("Tiếp tân hủy lịch Scheduled hoặc CheckedIn (bắt buộc lý do); hoàn tiền mặt tại quầy (Staff)")
            .Produces<CancelResponse>().Produces<OperationAccepted>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

    private static async Task<IResult> StaffCancelAsync(Guid appointmentId, ClaimsPrincipal user, HttpRequest request,
        [FromServices] AppointmentChangeService changes, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();
        var (key, body, error) = await ReadAsync<CancelBody>(request, allowEmptyBody: true, ct);
        if (error is not null)
            return error;

        var outcome = await changes.CancelAsync(new CancelRequest(appointmentId, userId, CancellationInitiator.Receptionist,
            null, string.IsNullOrWhiteSpace(body?.Reason) ? null : body.Reason.Trim(), null, key!), ct);
        return outcome switch
        {
            { Result: ChangeResult.Cancelled, Cancelled: { } c } => TypedResults.Ok(new CancelResponse(appointmentId,
                "Cancelled", c.SlotReleased ? "Full" : "None", c.SlotReleased ? c.AppliedConsultationFee : 0m, "VND",
                c.CancelledAtUtc, outcome.OperationId)),
            _ => ChangeProblem(outcome),
        };
    }

    // Cùng mốc với lệnh hủy: trước RescheduleCutoffTimeUtc (05:00 ngày D) theo giờ DB thì hoàn toàn bộ, nhận tiền mặt tại quầy.
    private static async Task<IResult> PreviewCancelAsync(Guid appointmentId, ClaimsPrincipal user,
        [FromServices] IPatientAppointmentReader reader, [FromServices] IIdentityPatientsModule identity,
        [FromServices] IDbClock clock, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();
        var row = await OwnedAsync(appointmentId, userId, reader, identity, ct);
        if (row is null)
            return NotFound();
        if (row.Status != AppointmentStatus.Scheduled || row.RescheduleCutoffTimeUtc is not { } cutoff)
            return Problem(StatusCodes.Status409Conflict, nameof(ChangeResult.InvalidAppointmentState), "Lịch hẹn không hủy được.");

        var full = await clock.GetUtcNowAsync(ct) < cutoff;
        return TypedResults.Ok(new CancellationPreview(appointmentId, full ? "Full" : "None",
            full ? row.AppliedConsultationFee : 0m, "VND", cutoff, full ? "Counter" : null));
    }

    private static async Task<IResult> RescheduleAsync(Guid appointmentId, ClaimsPrincipal user, HttpRequest request,
        [FromServices] IPatientAppointmentReader reader, [FromServices] IIdentityPatientsModule identity,
        [FromServices] AppointmentChangeService changes, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();
        var (key, body, error) = await ReadAsync<RescheduleBody>(request, allowEmptyBody: false, ct);
        if (error is not null)
            return error;
        if (body is null || body.TargetDoctorId == Guid.Empty || body.TargetServiceDate == DateOnly.MinValue)
            return Invalid();
        var row = await OwnedAsync(appointmentId, userId, reader, identity, ct);
        if (row is null)
            return NotFound();
        // Hạng bác sĩ đích lấy phía server để báo giá; client không gửi.
        if (await identity.GetDoctorAsync(body.TargetDoctorId, ct) is not { IsActive: true } doctor)
            return Problem(StatusCodes.Status404NotFound, "DoctorNotFound", "Không tìm thấy bác sĩ đích.");

        var outcome = await changes.RescheduleAsync(new RescheduleRequest(appointmentId, userId, row.PatientId,
            doctor.DoctorId, doctor.RankId, body.TargetServiceDate, null, key!), ct);
        if (outcome is not { Result: ChangeResult.Rescheduled, Rescheduled: { } n })
            return ChangeProblem(outcome);
        var created = await reader.GetAsync(n.AppointmentId, userId, ct);
        return created is null ? NotFound() : TypedResults.Ok(await ToItemAsync(created, identity, [], ct));
    }

    // Lịch do người gọi đặt và người gọi còn quyền Booking trên hồ sơ; khác thì cùng 404 (không lộ lịch người khác).
    private static async Task<PatientAppointment?> OwnedAsync(Guid appointmentId, Guid userId, IPatientAppointmentReader reader,
        IIdentityPatientsModule identity, CancellationToken ct) =>
        await reader.GetAsync(appointmentId, userId, ct) is { } row
            && await identity.CanAccessPatientAsync(userId, row.PatientId, AccessLevel.Booking, ct) ? row : null;

    private static async Task<(string? Key, T? Body, IResult? Error)> ReadAsync<T>(HttpRequest request, bool allowEmptyBody,
        CancellationToken ct) where T : class
    {
        var keys = request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || string.IsNullOrWhiteSpace(keys[0]))
            return (null, null, Invalid());
        if (allowEmptyBody && request.ContentLength is 0 or null && !request.HasJsonContentType())
            return (keys[0], null, null);
        if (!request.HasJsonContentType())
            return (null, null, Problem(StatusCodes.Status415UnsupportedMediaType, "UnsupportedMediaType", "Yêu cầu JSON."));
        try
        {
            return (keys[0], await request.ReadFromJsonAsync<T>(ct), null);
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, null, Invalid());
        }
    }

    private static IResult ChangeProblem(ChangeOutcome outcome) => outcome switch
    {
        { Result: ChangeResult.InProgress, OperationId: { } id } => TypedResults.Accepted((string?)null, new OperationAccepted(id)),
        { Result: ChangeResult.InProgress } => Problem(StatusCodes.Status409Conflict, "RequestInProgress", "Lệnh đang xử lý."),
        _ => Problem(AppointmentChangeService.StatusCode(outcome.Result), outcome.Result.ToString(), "Không thể thay đổi lịch hẹn."),
    };

    private static IResult Invalid() => Problem(StatusCodes.Status400BadRequest, "InvalidInput", "Yêu cầu không hợp lệ.");

    private static IResult NotFound() =>
        Problem(StatusCodes.Status404NotFound, "AppointmentNotFound", "Không tìm thấy lịch hẹn.");

    private static IResult Problem(int status, string code, string title) => TypedResults.Problem(statusCode: status,
        title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, [FromServices] IPatientAppointmentReader reader,
        [FromServices] IIdentityPatientsModule identity, CancellationToken ct, string page = "1", string pageSize = "20")
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        if (!int.TryParse(page, CultureInfo.InvariantCulture, out var pageNumber) || pageNumber is < 1 or > MaxPage
            || !int.TryParse(pageSize, CultureInfo.InvariantCulture, out var size) || size is < 1 or > MaxPageSize)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Phân trang không hợp lệ.",
                extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
        }

        var rows = await reader.ListAsync(userId, (pageNumber - 1) * size, size + 1, ct);
        var pageRows = rows.Take(size).ToList();
        // Quyền Booking có thể đã bị thu hồi sau khi đặt: không trả lịch của hồ sơ đó. Một truy vấn cho cả trang.
        var allowed = await identity.GetAccessiblePatientsAsync(userId, pageRows.Select(x => x.PatientId).ToList(), AccessLevel.Booking, ct);
        var doctors = pageRows.Count == 0 ? new Dictionary<Guid, string?>() : (await identity.SearchDoctorsAsync(new DoctorSearch(), ct))
            .ToDictionary(x => x.DoctorId, x => (string?)x.FullName);
        var items = new List<AppointmentItem>(pageRows.Count);
        foreach (var row in pageRows.Where(x => allowed.Contains(x.PatientId)))
            items.Add(await ToItemAsync(row, identity, doctors, ct));

        return TypedResults.Ok(new AppointmentPage(pageNumber, size, rows.Count > size, items));
    }

    private static async Task<IResult> GetAsync(Guid appointmentId, ClaimsPrincipal user,
        [FromServices] IPatientAppointmentReader reader, [FromServices] IIdentityPatientsModule identity, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        var row = await reader.GetAsync(appointmentId, userId, ct);
        return row is not null && await identity.CanAccessPatientAsync(userId, row.PatientId, AccessLevel.Booking, ct)
            ? TypedResults.Ok(await ToItemAsync(row, identity, [], ct))
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Không tìm thấy lịch hẹn.",
                extensions: new Dictionary<string, object?> { ["code"] = "AppointmentNotFound" });
    }

    private static async Task<AppointmentItem> ToItemAsync(PatientAppointment row, IIdentityPatientsModule identity,
        Dictionary<Guid, string?> doctors, CancellationToken ct)
    {
        if (!doctors.TryGetValue(row.DoctorId, out var doctorName))
            doctors[row.DoctorId] = doctorName = (await identity.GetDoctorAsync(row.DoctorId, ct))?.FullName;

        return new AppointmentItem(row.AppointmentId, row.AppointmentNumber, row.Status.ToString(), row.PatientId, row.DoctorId,
            doctorName, row.ServiceDate, row.AssignedRoomId, row.AppliedConsultationFee, "VND", row.ConfirmedAtUtc,
            row.CheckInDeadlineUtc, row.CancelledAtUtc);
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid id) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id) && id != Guid.Empty;

    private sealed record CancelBody(string? Reason);
    private sealed record RescheduleBody(Guid TargetDoctorId, DateOnly TargetServiceDate);
    private sealed record OperationAccepted(Guid OperationId);
    private sealed record CancellationPreview(Guid AppointmentId, string Refund, decimal RefundAmount, string Currency,
        DateTime RefundCutoffUtc, string? RefundMethod);
    private sealed record CancelResponse(Guid AppointmentId, string Status, string Refund, decimal RefundAmount, string Currency,
        DateTime CancelledAtUtc, Guid? OperationId);

    private sealed record AppointmentPage(int Page, int PageSize, bool HasMore, IReadOnlyList<AppointmentItem> Items);

    private sealed record AppointmentItem(Guid AppointmentId, string AppointmentNumber, string Status, Guid PatientId,
        Guid DoctorId, string? DoctorName, DateOnly ServiceDate, Guid AssignedRoomId, decimal AppliedConsultationFee,
        string Currency, DateTime ConfirmedAtUtc, DateTime? CheckInDeadlineUtc, DateTime? CancelledAtUtc);
}
