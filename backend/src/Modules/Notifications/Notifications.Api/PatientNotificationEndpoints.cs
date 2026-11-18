using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Notifications.Application.PatientNotifications;

namespace Notifications.Api;

internal static class PatientNotificationEndpoints
{
    public static void MapPatientNotificationRoutes(this RouteGroupBuilder notifications)
    {
        var group = notifications.MapGroup("/patient/notifications").RequireAuthorization("PatientOnly").AddEndpointFilter(Errors);
        group.MapGet("", (ClaimsPrincipal user, Guid patientId, bool? unreadOnly, int? page, int? pageSize,
            [FromServices] IPatientNotificationReader reader, CancellationToken ct) =>
            reader.ListAsync(Actor(user), patientId, unreadOnly ?? false, page ?? 1, pageSize ?? 50, ct))
            .WithSummary("Thông báo của hồ sơ đang chọn (Patient)").Produces<PatientNotificationPage>();
        group.MapPost("/{id:guid}/read", async (Guid id, ClaimsPrincipal user, [FromServices] IPatientNotificationReader reader, CancellationToken ct) =>
            await reader.MarkReadAsync(Actor(user), id, ct) ? (IResult)TypedResults.NoContent() : Problem(PatientNotificationError.NotFound))
            .WithSummary("Đánh dấu đã đọc riêng cho tài khoản hiện tại (Patient)").Produces(StatusCodes.Status204NoContent);
    }
    private static Guid Actor(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id)
        && id != Guid.Empty ? id : throw new UnauthorizedAccessException();
    private static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (UnauthorizedAccessException) { return TypedResults.Unauthorized(); }
        catch (PatientNotificationException error) { return Problem(error.Error); }
    }
    private static IResult Problem(PatientNotificationError error) => TypedResults.Problem(statusCode: error switch
    {
        PatientNotificationError.InvalidInput => 400,
        PatientNotificationError.NotFound => 404,
        PatientNotificationError.IdempotencyConflict => 409,
        _ => throw new InvalidOperationException("Unknown notification error.")
    }, title: error.ToString(), extensions: new Dictionary<string, object?> { ["code"] = error.ToString() });
}
