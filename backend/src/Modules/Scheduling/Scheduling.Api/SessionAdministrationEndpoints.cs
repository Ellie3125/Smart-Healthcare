using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.SessionAdministration;

namespace Scheduling.Api;

internal static class SessionAdministrationEndpoints
{
    public static void MapSessionAdministrationRoutes(this RouteGroupBuilder scheduling)
    {
        var admin = scheduling.MapGroup("/admin").RequireAuthorization("AdminOnly").AddEndpointFilter(Errors);
        admin.MapPost("/doctor-days/{doctorId:guid}/{serviceDate}/replace-doctor", (Guid doctorId, DateOnly serviceDate, ReplaceDoctorRequest body,
            ClaimsPrincipal user, [FromServices] ISessionAdministration service, CancellationToken ct) =>
            service.ReplaceDoctorAsync(doctorId, serviceDate, body.ReplacementDoctorId, Actor(user), body.Reason, ct));
        admin.MapPost("/doctor-days/{doctorId:guid}/{serviceDate}/cancel-session", (Guid doctorId, DateOnly serviceDate, ReasonRequest body,
            ClaimsPrincipal user, [FromServices] ISessionAdministration service, CancellationToken ct) =>
            service.CancelSessionAsync(doctorId, serviceDate, Actor(user), body.Reason, ct));
        admin.MapPost("/rooms/{roomId:guid}/disable", (Guid roomId, ReasonRequest body, ClaimsPrincipal user,
            [FromServices] ISessionAdministration service, CancellationToken ct) => service.DisableRoomAsync(roomId, Actor(user), body.Reason, ct));
        scheduling.MapGet("/staff/call-list", (int? page, int? pageSize, [FromServices] ISessionAdministration service, CancellationToken ct) =>
            service.GetCallListAsync(page ?? 1, pageSize ?? 50, ct)).RequireAuthorization("ReceptionistOrAdmin").AddEndpointFilter(Errors);
    }
    private static Guid Actor(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id)
        && id != Guid.Empty ? id : throw new UnauthorizedAccessException();
    private static async ValueTask<object?> Errors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try { return await next(context); }
        catch (UnauthorizedAccessException) { return TypedResults.Unauthorized(); }
        catch (SessionAdminException error)
        {
            var status = error.Error switch
            {
                SessionAdminError.InvalidInput => 400,
                SessionAdminError.DayNotFound or SessionAdminError.DoctorUnavailable or SessionAdminError.RoomNotFound => 404,
                SessionAdminError.AlreadyHasSession or SessionAdminError.SessionStarted or SessionAdminError.AlreadyCheckedIn or SessionAdminError.InvalidState => 409,
                _ => throw new InvalidOperationException($"Unexpected session admin result {error.Error}")
            };
            return TypedResults.Problem(statusCode: status, title: error.Error.ToString(), extensions: new Dictionary<string, object?> { ["code"] = error.Error.ToString() });
        }
    }
    private sealed record ReplaceDoctorRequest(Guid ReplacementDoctorId, string Reason);
    private sealed record ReasonRequest(string Reason);
}
