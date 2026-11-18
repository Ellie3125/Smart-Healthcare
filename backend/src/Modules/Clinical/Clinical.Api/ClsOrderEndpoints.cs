using System.Security.Claims;
using System.Text.Json;
using Billing.Contracts;
using Clinical.Application.Orders;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Contracts;

namespace Clinical.Api;

public static class ClsOrderEndpoints
{
    public static void MapClsOrderRoutes(this RouteGroupBuilder clinical) =>
        clinical.MapPost("/staff/doctor/encounters/{encounterId:guid}/cls-orders", OrderAsync).RequireAuthorization(Policies.DoctorOnly);
    private static async Task<IResult> OrderAsync(Guid encounterId, HttpRequest request, ClaimsPrincipal user,
        [FromServices] ClsOrderService service, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var userId) || userId == Guid.Empty
            || !Guid.TryParse(user.FindFirstValue(AppClaims.DoctorId), out var doctorId) || doctorId == Guid.Empty) return TypedResults.Forbid();
        if (!request.HasJsonContentType()) return Problem(415, "UnsupportedMediaType");
        if (!request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1) return Problem(400, "InvalidInput");
        ClsOrderRequest? body;
        try { body = await request.ReadFromJsonAsync<ClsOrderRequest>(ct); }
        catch (JsonException) { return Problem(400, "InvalidInput"); }
        catch (BadHttpRequestException) { return Problem(400, "InvalidInput"); }
        if (body is null) return Problem(400, "InvalidInput");
        try { return TypedResults.Created((string?)null, await service.OrderAsync(encounterId, doctorId, userId, body, keys[0]!, ct)); }
        catch (ClsOrderException ex) { return Problem(ex.Error switch { ClsOrderError.InvalidInput => 400, ClsOrderError.Forbidden => 403,
            ClsOrderError.EncounterNotFound => 404, _ => 409 }, ex.Error.ToString()); }
        catch (BillingException ex) { return Problem(409, ex.Code.ToString()); }
        catch (SchedulingException ex) { return Problem(409, ex.Code.ToString()); }
    }
    private static IResult Problem(int status, string code) => TypedResults.Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
