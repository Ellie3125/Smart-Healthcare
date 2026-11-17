using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using IdentityPatients.Contracts;
using Scheduling.Application.RoomQueues;

namespace Scheduling.Api;

public static class RoomQueueEndpoints
{
    public static RouteGroupBuilder MapSchedulingRoomQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scheduling").WithTags(DependencyInjection.Tag);
        group.MapRoomQueueRoutes();
        return group;
    }

    internal static void MapRoomQueueRoutes(this RouteGroupBuilder group)
    {
        group.MapGet("/staff/rooms/{roomId:guid}/queue", StaffAsync)
            .RequireAuthorization(Policies.AnyChannel).Produces<RoomQueuePage>().ProducesProblem(400);
        group.MapGet("/public/display", PublicAsync).AllowAnonymous()
            .Produces<PublicRoomDisplay>().ProducesProblem(400);
    }

    private static async Task<IResult> StaffAsync(Guid roomId, ClaimsPrincipal user,
        [FromServices] IRoomQueueReader reader, CancellationToken ct, string page = "1", string pageSize = "50")
    {
        if (!user.HasClaim(AppClaims.Channel, Channels.Staff)
            || !(user.IsInRole(Roles.Receptionist) || user.IsInRole(Roles.Nurse)
                || user.IsInRole(Roles.Doctor) || user.IsInRole(Roles.RoomAccount)))
            return TypedResults.Forbid();
        if (roomId == Guid.Empty || !int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number is < 1 or > 1000 || !int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out var size)
            || size is < 1 or > 100)
            return InvalidInput();
        if (user.IsInRole(Roles.RoomAccount)
            && (!Guid.TryParse(user.FindFirstValue(AppClaims.RoomId), out var assigned) || assigned != roomId))
            return TypedResults.Forbid();
        Guid? doctorId = null;
        if (user.IsInRole(Roles.Doctor))
        {
            if (!Guid.TryParse(user.FindFirstValue(AppClaims.DoctorId), out var doctor) || doctor == Guid.Empty)
                return TypedResults.Forbid();
            doctorId = doctor;
        }
        var result = await reader.GetStaffAsync(roomId, doctorId, number, size, ct);
        return result is null ? TypedResults.Forbid() : TypedResults.Ok(result);
    }

    private static async Task<IResult> PublicAsync([FromServices] IRoomQueueReader reader, CancellationToken ct,
        string? roomId = null) => Guid.TryParse(roomId, out var room) && room != Guid.Empty
            ? TypedResults.Ok(await reader.GetPublicAsync(room, ct)) : InvalidInput();

    private static IResult InvalidInput() => TypedResults.Problem(statusCode: 400, title: "Phòng hoặc phân trang không hợp lệ.",
        extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
}
