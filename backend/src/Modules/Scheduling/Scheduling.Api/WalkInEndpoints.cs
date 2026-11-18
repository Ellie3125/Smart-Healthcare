using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.WalkIn;
using Scheduling.Contracts;
using Billing.Contracts;

namespace Scheduling.Api;

public static class WalkInEndpoints
{
    public static RouteGroupBuilder MapSchedulingWalkInEndpoints(this IEndpointRouteBuilder app)
    {
        // Tra hồ sơ trước khi đăng ký tại quầy: khớp chính xác SĐT hoặc CCCD, dữ liệu nhận dạng đã che.
        app.MapGet("/api/scheduling/staff/reception/search", SearchAsync).WithTags(DependencyInjection.Tag)
            .RequireAuthorization(Policies.ReceptionistOnly)
            .Produces<IReadOnlyList<ReceptionPatientDto>>().ProducesProblem(400);
        var group = app.MapGroup("/api/scheduling/staff/walk-in").WithTags(DependencyInjection.Tag)
            .RequireAuthorization(Policies.ReceptionistOnly);
        group.MapPost("", RegisterAsync).Produces<WalkInOutcome>(201)
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        return group;
    }

    private static async Task<IResult> RegisterAsync(RegisterWalkIn body, ClaimsPrincipal user, HttpRequest request,
        [FromServices] WalkInService service, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor)
            || actor == Guid.Empty)
            return TypedResults.Unauthorized();
        var keys = request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || string.IsNullOrWhiteSpace(keys[0]) || keys[0]!.Length > 64)
            return Problem("InvalidInput", 400);
        try
        {
            var result = await service.RegisterAsync(new(body.PatientId, body.DoctorId, actor, keys[0]!), ct);
            var status = WalkInService.StatusCode(result);
            return result.Result == WalkInResult.Registered ? TypedResults.Json(result, statusCode: status)
                : Problem(result.HoldFailure?.ToString() ?? result.Result.ToString(), status);
        }
        catch (SchedulingException error)
        {
            return Problem(error.Code.ToString(), error.Code == SchedulingErrorCode.AppointmentNotFound ? 404 : 409);
        }
        catch (BillingException error)
        {
            return Problem(error.Code.ToString(), error.Code == BillingErrorCode.InvalidRequest ? 400 : 409);
        }
    }

    private static async Task<IResult> SearchAsync(string? keyword, [FromServices] IIdentityPatientsModule identity,
        CancellationToken ct)
    {
        var key = string.Concat((keyword ?? "").Where(c => !char.IsWhiteSpace(c)));
        if (key.Length is < 9 or > 20)
            return TypedResults.Problem(statusCode: 400, title: "Nhập số điện thoại hoặc CCCD.",
                extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
        return TypedResults.Ok(await identity.FindPatientsForReceptionAsync(key, ct));
    }

    private static IResult Problem(string code, int status) => TypedResults.Problem(statusCode: status,
        title: "Không thể đăng ký tại quầy.", extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed record RegisterWalkIn(Guid PatientId, Guid DoctorId);
}
