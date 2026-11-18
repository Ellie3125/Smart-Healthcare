using System.Security.Claims;
using System.Text.Json;
using Clinical.Application.Results;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Clinical.Api;

public static class RoomResultEndpoints
{
    public static void MapRoomResultRoutes(this RouteGroupBuilder clinical)
    {
        var room = clinical.MapGroup("/staff/room/results").RequireAuthorization(Policies.RoomAccountOnly);
        room.MapGet("/{orderItemId:guid}", GetAsync).WithSummary("Đọc kết quả nháp của phòng CLS");
        room.MapPut("/{orderItemId:guid}", SaveAsync).WithSummary("Lưu kết quả nháp của phòng CLS");
        room.MapPost("/{orderItemId:guid}/publish", PublishAsync).WithSummary("Trả kết quả CLS");
    }
    private static async Task<IResult> GetAsync(Guid orderItemId, ClaimsPrincipal user,
        [FromServices] ClsPublicationService service, CancellationToken ct)
    {
        if (Principal(user, out _, out var roomId) is { } denied) return denied;
        try { return TypedResults.Ok(await service.GetDraftAsync(orderItemId, roomId, ct)); }
        catch (ClsPublicationException error) { return Error(error); }
    }
    private static async Task<IResult> SaveAsync(Guid orderItemId, ClaimsPrincipal user, HttpRequest request,
        [FromServices] ClsPublicationService service, CancellationToken ct)
    {
        if (Principal(user, out var actorId, out var roomId) is { } denied) return denied;
        if (!request.HasJsonContentType()) return Problem(415, "UnsupportedMediaType");
        ClsDraftRequest? body;
        try { body = await request.ReadFromJsonAsync<ClsDraftRequest>(ct); }
        catch (JsonException) { return Problem(400, "InvalidInput"); }
        catch (BadHttpRequestException) { return Problem(400, "InvalidInput"); }
        if (body is null) return Problem(400, "InvalidInput");
        try { return TypedResults.Ok(await service.SaveDraftAsync(orderItemId, roomId, actorId, body, ct)); }
        catch (ClsPublicationException error) { return Error(error); }
    }
    private static async Task<IResult> PublishAsync(Guid orderItemId, ClaimsPrincipal user, HttpRequest request,
        [FromServices] ClsPublicationService service, CancellationToken ct)
    {
        if (Principal(user, out var actorId, out var roomId) is { } denied) return denied;
        if (!request.HasJsonContentType()) return Problem(415, "UnsupportedMediaType");
        if (!request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1) return Problem(400, "InvalidInput");
        ClsPublishRequest? body;
        try { body = await request.ReadFromJsonAsync<ClsPublishRequest>(ct); }
        catch (JsonException) { return Problem(400, "InvalidInput"); }
        catch (BadHttpRequestException) { return Problem(400, "InvalidInput"); }
        if (body is null) return Problem(400, "InvalidInput");
        try { return TypedResults.Ok(await service.PublishAsync(orderItemId, roomId, actorId, body, keys[0]!, ct)); }
        catch (ClsPublicationException error) { return Error(error); }
    }
    private static IResult? Principal(ClaimsPrincipal user, out Guid actorId, out Guid roomId)
    {
        roomId = Guid.Empty;
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out actorId) || actorId == Guid.Empty)
            return TypedResults.Unauthorized();
        if (!Guid.TryParse(user.FindFirstValue(AppClaims.RoomId), out roomId) || roomId == Guid.Empty) return TypedResults.Forbid();
        return null;
    }
    private static IResult Error(ClsPublicationException error) => Problem(error.Error switch
    {
        ClsPublicationError.InvalidInput => 400,
        ClsPublicationError.Forbidden => 403,
        ClsPublicationError.ResultNotFound => 404,
        _ => 409
    }, error.Error.ToString());
    private static IResult Problem(int status, string code) => TypedResults.Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
