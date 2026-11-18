using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using IdentityPatients.Contracts;
using Scheduling.Application.RoomQueues;
using Clinical.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Shared.Abstractions;

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
        group.MapGet("/staff/my-rooms", MyRoomsAsync)
            .RequireAuthorization(Policies.AnyChannel).Produces<IReadOnlyList<StaffRoomOption>>();
        group.MapGet("/public/display", PublicAsync).AllowAnonymous()
            .Produces<PublicRoomDisplay>().ProducesProblem(400);
    }

    private static async Task<IResult> StaffAsync(Guid roomId, ClaimsPrincipal user,
        [FromServices] IRoomQueueReader reader, IServiceProvider services, CancellationToken ct, string page = "1", string pageSize = "50")
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
        if (result is null) return TypedResults.Forbid();
        if (user.IsInRole(Roles.Nurse))
        {
            var now = await services.GetRequiredService<IDbClock>().GetUtcNowAsync(ct);
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor)
                || result.ServiceDate != DateOnly.FromDateTime(now.AddHours(7))
                || !await services.GetRequiredService<IIdentityPatientsModule>().IsNurseOnDutyAsync(actor, roomId, now, ct))
                return TypedResults.Forbid();
        }
        var ids = result.Items.Where(t => t.EncounterId.HasValue).Select(t => t.EncounterId!.Value).Distinct().ToArray();
        if (ids.Length > 0)
        {
            var contexts = (await services.GetRequiredService<IClinicalModule>().GetQueuePatientContextsAsync(ids, ct)).ToDictionary(c => c.EncounterId);
            var names = (await services.GetRequiredService<IIdentityPatientsModule>().GetPatientContactsAsync(contexts.Values.Select(c => c.PatientId).Distinct().ToArray(), ct))
                .ToDictionary(p => p.PatientId, p => p.FullName);
            result = result with { Items = result.Items.Select(t => t with { PatientName = t.EncounterId is { } id && contexts.TryGetValue(id, out var context)
                ? names.GetValueOrDefault(context.PatientId, "") : "" }).ToArray() };
        }
        return TypedResults.Ok(result);
    }

    /// <summary>Phòng người gọi được xem hàng chờ hôm nay, cùng luật với StaffAsync: RoomAccount theo token,
    /// Doctor theo phân công ngày, Nurse theo ca trực, Receptionist mọi phòng có phiên.</summary>
    private static async Task<IResult> MyRoomsAsync(ClaimsPrincipal user, [FromServices] IRoomQueueReader reader,
        IServiceProvider services, CancellationToken ct)
    {
        if (!user.HasClaim(AppClaims.Channel, Channels.Staff))
            return TypedResults.Forbid();
        IReadOnlyList<Guid> rooms;
        if (user.IsInRole(Roles.RoomAccount))
            rooms = Guid.TryParse(user.FindFirstValue(AppClaims.RoomId), out var own) && own != Guid.Empty ? [own] : [];
        else if (user.IsInRole(Roles.Doctor))
            rooms = Guid.TryParse(user.FindFirstValue(AppClaims.DoctorId), out var doctor) && doctor != Guid.Empty
                ? await reader.GetTodayRoomIdsAsync(doctor, ct) : [];
        else if (user.IsInRole(Roles.Nurse))
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor))
                return TypedResults.Forbid();
            var identity = services.GetRequiredService<IIdentityPatientsModule>();
            var now = await services.GetRequiredService<IDbClock>().GetUtcNowAsync(ct);
            var onDuty = new List<Guid>();
            foreach (var room in await reader.GetTodayRoomIdsAsync(null, ct))
                if (await identity.IsNurseOnDutyAsync(actor, room, now, ct)) onDuty.Add(room);
            rooms = onDuty;
        }
        else if (user.IsInRole(Roles.Receptionist))
            rooms = await reader.GetTodayRoomIdsAsync(null, ct);
        else
            return TypedResults.Forbid();
        if (rooms.Count == 0) return TypedResults.Ok(Array.Empty<StaffRoomOption>());
        var names = (await services.GetRequiredService<IIdentityPatientsModule>().GetRoomsAsync(ct: ct)).ToDictionary(r => r.Id, r => r.Name);
        return TypedResults.Ok(rooms.Select(r => new StaffRoomOption(r, names.GetValueOrDefault(r, ""))).OrderBy(r => r.RoomName).ToArray());
    }

    private static async Task<IResult> PublicAsync([FromServices] IRoomQueueReader reader, CancellationToken ct,
        string? roomId = null) => Guid.TryParse(roomId, out var room) && room != Guid.Empty
            ? TypedResults.Ok(await reader.GetPublicAsync(room, ct)) : InvalidInput();

    private static IResult InvalidInput() => TypedResults.Problem(statusCode: 400, title: "Phòng hoặc phân trang không hợp lệ.",
        extensions: new Dictionary<string, object?> { ["code"] = "InvalidInput" });
}
