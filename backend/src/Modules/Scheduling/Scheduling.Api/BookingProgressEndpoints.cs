using System.Security.Claims;
using System.Text.Json;
using Billing.Contracts;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.BookingAttempts;
using Scheduling.Application.Holds;

namespace Scheduling.Api;

internal static class BookingProgressEndpoints
{
    public static void MapBookingProgressEndpoints(this RouteGroupBuilder scheduling)
    {
        var patient = scheduling.MapGroup("/patient").RequireAuthorization(Policies.PatientOnly);
        patient.MapPost("/booking-attempts", CreateAttemptAsync)
            .WithSummary("Tạo tiến trình đặt lịch cho một hồ sơ (Patient)")
            .Accepts<CreateAttemptBody>("application/json")
            .Produces<AttemptCreated>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
        patient.MapPost("/booking-attempts/{bookingAttemptId:guid}/payment", StartPaymentAsync)
            .WithSummary("Tạo yêu cầu thanh toán phí khám cho suất đang giữ (Patient). Gọi lại trả cùng yêu cầu.")
            .Produces<PaymentStarted>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        patient.MapGet("/booking-attempts/{bookingAttemptId:guid}", GetAttemptAsync)
            .WithSummary("Khôi phục tiến trình đặt lịch (Patient)")
            .Produces<BookingProgress>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        patient.MapGet("/operations/{operationId:guid}", GetOperationAsync)
            .WithSummary("Tra trạng thái lệnh đặt lịch (Patient)")
            .Produces<BookingOperationProgress>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        patient.MapPost("/booking-attempts/{bookingAttemptId:guid}/hold", HoldAsync)
            .WithSummary("Giữ chỗ và trả mã tra cứu tiến trình (Patient)")
            .Accepts<HoldBody>("application/json")
            .Produces<HoldResponse>(StatusCodes.Status201Created)
            .Produces<OperationAccepted>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);
    }

    private static async Task<IResult> CreateAttemptAsync(ClaimsPrincipal user, HttpRequest request,
        [FromServices] BookingAttemptService attempts, [FromServices] IIdentityPatientsModule identity, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        var keys = request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || string.IsNullOrWhiteSpace(keys[0]))
            return InvalidInput();

        if (!request.HasJsonContentType())
            return Problem(StatusCodes.Status415UnsupportedMediaType, "UnsupportedMediaType", "Yêu cầu JSON.");

        CreateAttemptBody? body;
        try
        {
            body = await request.ReadFromJsonAsync<CreateAttemptBody>(ct);
        }
        catch (JsonException)
        {
            return InvalidInput();
        }

        if (body is null || body.PatientId == Guid.Empty || body.ServiceDate == DateOnly.MinValue)
            return InvalidInput();

        // Hồ sơ không tồn tại và hồ sơ người gọi không được đặt lịch trả cùng 404.
        if (!await identity.CanAccessPatientAsync(userId, body.PatientId, AccessLevel.Booking, ct))
            return Problem(StatusCodes.Status404NotFound, "PatientNotFound", "Không tìm thấy hồ sơ bệnh nhân.");

        var outcome = await attempts.CreateAsync(new CreateAttemptRequest(userId, body.PatientId, body.ServiceDate, keys[0]!), ct);
        return outcome switch
        {
            { Result: CreateAttemptResult.Created, BookingAttemptId: { } id } => TypedResults.Created(
                $"/api/scheduling/patient/booking-attempts/{id:D}", new AttemptCreated(id)),
            { Result: CreateAttemptResult.InProgress } => Problem(StatusCodes.Status409Conflict, "RequestInProgress", "Lệnh đang xử lý."),
            { Result: CreateAttemptResult.IdempotencyConflict } => Problem(StatusCodes.Status409Conflict, "IdempotencyConflict",
                "Idempotency-Key đã dùng cho nội dung khác."),
            _ => InvalidInput(),
        };
    }

    private static async Task<IResult> StartPaymentAsync(Guid bookingAttemptId, ClaimsPrincipal user,
        [FromServices] BookingProgressService progress, [FromServices] BookingPaymentService payments,
        [FromServices] IIdentityPatientsModule identity, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        if (await progress.GetOwnedAttemptAsync(bookingAttemptId, userId, ct) is null)
            return AttemptNotFound();

        BookingPaymentOutcome outcome;
        try
        {
            outcome = await payments.StartAsync(bookingAttemptId, userId,
                async (doctorId, t) => (await identity.GetDoctorAsync(doctorId, t))?.FullName, ct);
        }
        catch (BillingException error) when (error.Code is BillingErrorCode.PriceNotConfigured or BillingErrorCode.AmbiguousPrice
            or BillingErrorCode.PaymentConflict or BillingErrorCode.ChargeNotPayable)
        {
            return Problem(StatusCodes.Status409Conflict, error.Code.ToString(), "Chưa tạo được yêu cầu thanh toán.");
        }

        return outcome.Result switch
        {
            BookingPaymentResult.Started => TypedResults.Ok(new PaymentStarted(outcome.PaymentIntentId!.Value,
                outcome.Amount!.Value, outcome.Currency!, outcome.ExpiresAtUtc!.Value)),
            BookingPaymentResult.AttemptNotFound => AttemptNotFound(),
            BookingPaymentResult.DoctorNotFound => Problem(StatusCodes.Status404NotFound, "DoctorNotFound", "Không tìm thấy bác sĩ."),
            BookingPaymentResult.InProgress => Problem(StatusCodes.Status409Conflict, "RequestInProgress", "Lệnh đang xử lý."),
            _ => Problem(StatusCodes.Status409Conflict, outcome.Result.ToString(), "Suất giữ không còn thanh toán được."),
        };
    }

    private static async Task<IResult> GetAttemptAsync(Guid bookingAttemptId, ClaimsPrincipal user,
        [FromServices] BookingProgressService service, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        var progress = await service.GetAttemptAsync(bookingAttemptId, userId, ct);
        return progress is null ? AttemptNotFound() : TypedResults.Ok(progress);
    }

    private static async Task<IResult> GetOperationAsync(Guid operationId, ClaimsPrincipal user,
        [FromServices] BookingProgressService service, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        var progress = await service.GetOperationAsync(operationId, userId, ct);
        return progress is null
            ? Problem(StatusCodes.Status404NotFound, "OperationNotFound", "Không tìm thấy lệnh.")
            : TypedResults.Ok(progress);
    }

    private static async Task<IResult> HoldAsync(Guid bookingAttemptId, ClaimsPrincipal user, HttpRequest request,
        [FromServices] BookingProgressService progress, [FromServices] HoldService holds,
        [FromServices] IIdentityPatientsModule identity, CancellationToken ct)
    {
        if (!TryGetUserId(user, out var userId))
            return TypedResults.Unauthorized();

        var keys = request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || string.IsNullOrWhiteSpace(keys[0]))
            return InvalidInput();

        if (!request.HasJsonContentType())
            return Problem(StatusCodes.Status415UnsupportedMediaType, "UnsupportedMediaType", "Yêu cầu JSON.");

        HoldBody? body;
        try
        {
            body = await request.ReadFromJsonAsync<HoldBody>(ct);
        }
        catch (JsonException)
        {
            return InvalidInput();
        }

        if (body is null)
            return InvalidInput();

        // shortcut: Quyền Booking có thể bị thu hồi giữa kiểm tra và ghi, kiểm lại dưới khóa trong UoW khi cần thu hồi nguyên tử.
        var attempt = await progress.GetOwnedAttemptAsync(bookingAttemptId, userId, ct);
        if (attempt is null)
            return AttemptNotFound();

        var outcome = await holds.HoldFromWebAsync(new WebHoldRequest(bookingAttemptId, attempt.PatientId,
            userId, body.DoctorId, body.ServiceDate, keys[0]!), async t =>
        {
            var doctor = await identity.GetDoctorAsync(body.DoctorId, t);
            return doctor is { IsActive: true } ? doctor.RankId : null;
        }, ct);

        return outcome switch
        {
            { Result: HoldResult.Held, OperationId: { } id } => TypedResults.Created(OperationLocation(id),
                new HoldResponse(id, outcome.ReservationId!.Value, outcome.ExpiresAtUtc!.Value)),
            { Result: HoldResult.InProgress, OperationId: { } id } => TypedResults.Accepted(OperationLocation(id),
                new OperationAccepted(id)),
            { Result: HoldResult.InProgress } => Problem(StatusCodes.Status409Conflict, "RequestInProgress", "Lệnh đang xử lý."),
            { Result: HoldResult.InvalidInput } => InvalidInput(),
            { Result: HoldResult.AttemptNotFound } => AttemptNotFound(),
            _ => Problem(HoldService.StatusCode(outcome.Result), outcome.Result.ToString(), "Không thể giữ chỗ."),
        };
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid id) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out id) && id != Guid.Empty;

    private static string OperationLocation(Guid id) => $"/api/scheduling/patient/operations/{id:D}";

    private static IResult AttemptNotFound() =>
        Problem(StatusCodes.Status404NotFound, "AttemptNotFound", "Không tìm thấy tiến trình đặt lịch.");

    private static IResult InvalidInput() =>
        Problem(StatusCodes.Status400BadRequest, "InvalidInput", "Thông tin giữ chỗ không hợp lệ.");

    private static IResult Problem(int statusCode, string code, string title) =>
        TypedResults.Problem(statusCode: statusCode, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed record HoldBody(Guid DoctorId, DateOnly ServiceDate);
    private sealed record CreateAttemptBody(Guid PatientId, DateOnly ServiceDate);
    private sealed record AttemptCreated(Guid BookingAttemptId);
    private sealed record PaymentStarted(Guid PaymentIntentId, decimal Amount, string Currency, DateTime ExpiresAtUtc);
    private sealed record HoldResponse(Guid OperationId, Guid ReservationId, DateTime ExpiresAtUtc);
    private sealed record OperationAccepted(Guid OperationId);
}
