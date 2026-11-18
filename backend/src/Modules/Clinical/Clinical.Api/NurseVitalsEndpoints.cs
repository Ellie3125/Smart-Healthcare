using System.Security.Claims;
using System.Text.Json;
using Clinical.Application.Vitals;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Clinical.Api;

public static class NurseVitalsEndpoints
{
    public static void MapNurseVitalsRoutes(this RouteGroupBuilder clinical) =>
        clinical.MapPost("/staff/nurse/encounters/{encounterId:guid}/vitals", RecordAsync)
            .RequireAuthorization(Policies.NurseOnly).WithSummary("Ghi một lần đo sinh hiệu (Staff Nurse)");

    private static async Task<IResult> RecordAsync(Guid encounterId, HttpRequest request, ClaimsPrincipal user,
        [FromServices] INurseStaffDirectory directory, [FromServices] RecordVitalsService service, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var userId)
            || userId == Guid.Empty) return TypedResults.Unauthorized();
        var staffId = await directory.GetActiveNurseStaffIdAsync(userId, ct);
        if (staffId is null || staffId == Guid.Empty) return TypedResults.Forbid();
        if (!request.HasJsonContentType()) return Problem(415, "UnsupportedMediaType");
        if (!request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1)
            return Problem(400, "InvalidInput");
        RecordVitalsRequest? body;
        try { body = await request.ReadFromJsonAsync<RecordVitalsRequest>(ct); }
        catch (JsonException) { return Problem(400, "InvalidInput"); }
        catch (BadHttpRequestException) { return Problem(400, "InvalidInput"); }
        if (body is null) return Problem(400, "InvalidInput");
        try
        {
            var result = await service.RecordAsync(encounterId, staffId.Value, userId, body, keys[0]!, ct);
            return TypedResults.Created((string?)null, result);
        }
        catch (VitalsException error)
        {
            return Problem(error.Error switch
            {
                VitalsError.InvalidInput => 400,
                VitalsError.EncounterNotFound => 404,
                _ => 409
            }, error.Error.ToString());
        }
    }

    private static IResult Problem(int status, string code) => TypedResults.Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
