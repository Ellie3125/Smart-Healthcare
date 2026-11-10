using System.Security.Claims;
using System.Text.Json;
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
    private sealed record HoldResponse(Guid OperationId, Guid ReservationId, DateTime ExpiresAtUtc);
    private sealed record OperationAccepted(Guid OperationId);
}
