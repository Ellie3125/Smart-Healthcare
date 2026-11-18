using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Notifications.Application.Exceptions;

namespace Notifications.Api;

internal static class WorkflowExceptionEndpoints
{
    public static void MapWorkflowExceptionRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflow/admin/exceptions").WithTags(DependencyInjection.Tag).RequireAuthorization("AdminOnly");
        group.MapGet("", async (int? take, [FromServices] IWorkflowExceptionMonitor monitor, CancellationToken ct) =>
        {
            if (take is < 1 or > 100) return (IResult)Problem(RetryExceptionResult.InvalidInput);
            return TypedResults.Ok(await monitor.GetAsync(take ?? 50, ct));
        }).WithSummary("Giám sát Operation treo và Outbox lỗi (Staff Admin)");
        group.MapPost("/outbox/{id:guid}/retry", async (Guid id, RetryRequest body, ClaimsPrincipal user, HttpRequest request,
            [FromServices] IWorkflowExceptionMonitor monitor, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor) || actor == Guid.Empty)
                return (IResult)TypedResults.Unauthorized();
            var result = await monitor.RetryOutboxAsync(id, actor, body.ResolutionNote, request.Headers["Idempotency-Key"].ToString(), ct);
            return result == RetryExceptionResult.Retried ? TypedResults.Ok(new { Status = result.ToString() }) : Problem(result);
        }).WithSummary("Cho thử lại DeadLetter sau kiểm tra thủ công (Staff Admin)");
    }

    private static IResult Problem(RetryExceptionResult result) => TypedResults.Problem(statusCode: result switch
    {
        RetryExceptionResult.InvalidInput => 400,
        RetryExceptionResult.NotFound => 404,
        RetryExceptionResult.Conflict or RetryExceptionResult.InProgress => 409,
        _ => throw new InvalidOperationException($"Unexpected retry result {result}.")
    }, title: result.ToString(), extensions: new Dictionary<string, object?> { ["code"] = result.ToString() });

    private sealed record RetryRequest(string ResolutionNote);
}
