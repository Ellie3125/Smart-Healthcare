using System.Security.Claims;
using System.Text.Json;
using Billing.Application;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Billing.Api;

internal static class AdminPriceListEndpoints
{
    public static void MapAdminPriceLists(this RouteGroupBuilder billing)
    {
        var group = billing.MapGroup("/admin/price-lists").RequireAuthorization(Policies.AdminOnly);
        group.MapGet("", async ([FromServices] PriceListAdminService service, HttpResponse response, CancellationToken ct, int page = 1, int pageSize = 50) =>
        {
            response.Headers.CacheControl = "no-store";
            try { return (IResult)TypedResults.Ok(await service.ListAsync(page, pageSize, ct)); }
            catch (PriceListAdminException e) { return Error(e.Error); }
        });
        group.MapPost("", CreateAsync);
    }

    private static async Task<IResult> CreateAsync(HttpRequest request, ClaimsPrincipal user,
        [FromServices] PriceListAdminService service, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor) || actor == Guid.Empty)
            return TypedResults.Forbid();
        if (!request.HasJsonContentType()) return Problem(415, "UnsupportedMediaType");
        string? key = null;
        if (request.Headers.TryGetValue("Idempotency-Key", out var keys))
        {
            if (keys.Count != 1) return Error(PriceListAdminError.InvalidInput);
            key = keys[0] ?? "";
        }
        CreatePriceListRequest? body;
        try { body = await request.ReadFromJsonAsync<CreatePriceListRequest>(ct); }
        catch (JsonException) { return Error(PriceListAdminError.InvalidInput); }
        catch (BadHttpRequestException) { return Error(PriceListAdminError.InvalidInput); }
        if (body is null) return Error(PriceListAdminError.InvalidInput);
        try { return TypedResults.Created((string?)null, await service.CreateAsync(body, actor, key, request.HttpContext.Connection.RemoteIpAddress?.ToString(), ct)); }
        catch (PriceListAdminException e) { return Error(e.Error); }
    }
    private static IResult Error(PriceListAdminError error) => Problem(error == PriceListAdminError.InvalidInput ? 400 : 409, error.ToString());
    private static IResult Problem(int status, string code) => TypedResults.Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
