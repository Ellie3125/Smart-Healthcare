using System.Security.Claims;
using Clinical.Application.Journey;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Clinical.Api;

internal static class PatientJourneyEndpoints
{
    public static void MapPatientJourneyRoutes(this RouteGroupBuilder group) => group.MapGet("/patient/today-journey", ReadAsync)
        .RequireAuthorization(Policies.PatientOnly).WithName("Clinical.Patient.TodayJourney");

    private static async Task<IResult> ReadAsync(HttpRequest request, ClaimsPrincipal user, TodayJourneyService service, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor) || actor == Guid.Empty) return Results.Unauthorized();
        Guid? patient = null;
        if (request.Query.TryGetValue("patientProfileId", out var values))
        {
            if (values.Count != 1 || !Guid.TryParse(values[0], out var id) || id == Guid.Empty)
                return Results.Problem(statusCode: 400, title: "Hồ sơ không hợp lệ.", extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
            patient = id;
        }
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        var result = await service.ReadAsync(actor, patient, ct);
        return result is null ? Results.Problem(statusCode: 404, title: "Không tìm thấy hồ sơ.", extensions: new Dictionary<string, object?> { ["code"] = "NotFound" }) : Results.Ok(result);
    }
}
