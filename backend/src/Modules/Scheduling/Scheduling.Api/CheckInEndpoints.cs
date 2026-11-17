using System.Globalization;
using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.CheckIn;
using Scheduling.Contracts;

namespace Scheduling.Api;

public static class CheckInEndpoints
{
    // Host chỉ gọi sau khi ClinicalDbContext đã tham gia UoW, tránh mở Encounter ngoài transaction check-in.
    public static RouteGroupBuilder MapSchedulingCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        var scheduling = app.MapGroup("/api/scheduling").WithTags(DependencyInjection.Tag);
        var staff = scheduling.MapGroup("/staff/appointments").RequireAuthorization(Policies.ReceptionistOnly);
        staff.MapPost("/{appointmentId:guid}/check-in", CheckInAsync)
            .WithSummary("Tiếp tân check-in sau khi đối chiếu giấy tờ (Staff)")
            .Produces<CheckInResult>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        staff.MapGet("/today", TodayAsync)
            .WithSummary("Tra lịch hẹn hôm nay theo giờ Việt Nam (Staff)")
            .Produces<TodayPage>().ProducesProblem(400);
        return scheduling;
    }

    private static async Task<IResult> CheckInAsync(Guid appointmentId, ClaimsPrincipal user,
        [FromServices] CheckInService checkIn, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var userId)
            || userId == Guid.Empty)
            return TypedResults.Unauthorized();
        try
        {
            return TypedResults.Ok(await checkIn.CheckInAsync(new CheckInAppointment(appointmentId, userId, null), ct));
        }
        catch (SchedulingException e)
        {
            return Problem(e.Code, e.Code switch
            {
                SchedulingErrorCode.InvalidInput => 400,
                SchedulingErrorCode.AppointmentNotFound => 404,
                _ => 409,
            });
        }
    }

    private static async Task<IResult> TodayAsync([FromServices] ICheckInStore store, CancellationToken ct,
        string page = "1", string pageSize = "20", string? doctorId = null, string? appointmentNumber = null)
    {
        if (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 1000
            || !int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out var size) || size is < 1 or > 50
            || (doctorId is not null && (!Guid.TryParse(doctorId, out var parsed) || parsed == Guid.Empty))
            || appointmentNumber is { Length: > 64 })
            return Problem(SchedulingErrorCode.InvalidInput, 400);
        var rows = await store.ListTodayAsync(doctorId is null ? null : Guid.Parse(doctorId), appointmentNumber,
            (number - 1) * size, size + 1, ct);
        return TypedResults.Ok(new TodayPage(number, size, rows.Count > size, rows.Take(size).ToArray()));
    }

    private static IResult Problem(SchedulingErrorCode code, int status) => TypedResults.Problem(statusCode: status,
        title: "Không thể xử lý lịch hẹn.", extensions: new Dictionary<string, object?> { ["code"] = code.ToString() });

    private sealed record TodayPage(int Page, int PageSize, bool HasMore, IReadOnlyList<TodayAppointment> Items);
}
