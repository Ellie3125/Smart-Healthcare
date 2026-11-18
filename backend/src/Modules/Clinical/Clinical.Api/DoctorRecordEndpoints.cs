using System.Security.Claims;
using System.Text.Json;
using Clinical.Application.Records;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Clinical.Api;

public static class DoctorRecordEndpoints
{
    public static void MapDoctorRecordRoutes(this RouteGroupBuilder clinical)
    {
        var group = clinical.MapGroup("/staff/doctor/encounters/{encounterId:guid}").RequireAuthorization(Policies.DoctorOnly);
        group.MapPut("/draft", (Guid encounterId, HttpRequest request, ClaimsPrincipal user, [FromServices] ClinicalRecordService service, CancellationToken ct) =>
            WriteAsync(encounterId, request, user, service, RecordAction.Draft, ct));
        group.MapPost("/publish", (Guid encounterId, HttpRequest request, ClaimsPrincipal user, [FromServices] ClinicalRecordService service, CancellationToken ct) =>
            WriteAsync(encounterId, request, user, service, RecordAction.Publish, ct));
        group.MapPost("/amend", (Guid encounterId, HttpRequest request, ClaimsPrincipal user, [FromServices] ClinicalRecordService service, CancellationToken ct) =>
            WriteAsync(encounterId, request, user, service, RecordAction.Amend, ct));
    }

    internal static async Task<IResult> ReadAsync(Guid id, ClaimsPrincipal user, [FromServices] ClinicalRecordService service, CancellationToken ct)
    {
        if (!TryActor(user, out _, out var doctorId)) return TypedResults.Forbid();
        try { return TypedResults.Ok(await service.ReadAsync(id, doctorId, ct)); }
        catch (ClinicalRecordException ex) { return Error(ex.Error); }
    }

    private static async Task<IResult> WriteAsync(Guid encounterId, HttpRequest request, ClaimsPrincipal user,
        ClinicalRecordService service, RecordAction action, CancellationToken ct)
    {
        if (!TryActor(user, out var userId, out var doctorId)) return TypedResults.Forbid();
        if (!request.HasJsonContentType()) return Problem(415, "UnsupportedMediaType");
        if (!request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1) return Problem(400, "InvalidInput");
        ClinicalRecordRequest? body;
        try { body = await request.ReadFromJsonAsync<ClinicalRecordRequest>(ct); }
        catch (JsonException) { return Problem(400, "InvalidInput"); }
        catch (BadHttpRequestException) { return Problem(400, "InvalidInput"); }
        if (body is null) return Problem(400, "InvalidInput");
        try { return TypedResults.Ok(await service.WriteAsync(encounterId, doctorId, userId, action, body, keys[0]!, ct)); }
        catch (ClinicalRecordException ex) { return Error(ex.Error); }
    }

    private static bool TryActor(ClaimsPrincipal user, out Guid userId, out Guid doctorId)
    {
        var validUser = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out userId) && userId != Guid.Empty;
        return Guid.TryParse(user.FindFirstValue(AppClaims.DoctorId), out doctorId) && doctorId != Guid.Empty && validUser;
    }
    private static IResult Error(RecordError error) => Problem(error switch
    {
        RecordError.InvalidInput => 400,
        RecordError.Forbidden => 403,
        RecordError.EncounterNotFound or RecordError.ClinicalRecordNotFound => 404,
        _ => 409
    }, error.ToString());
    private static IResult Problem(int status, string code) => TypedResults.Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
