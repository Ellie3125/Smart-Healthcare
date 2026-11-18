using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Scheduling.Application.Queues;
using Microsoft.Extensions.DependencyInjection;

namespace Scheduling.Api;

/// <summary>
/// Gọi người tiếp theo, bắt đầu, khám xong, trả về hàng chờ (W3-03). Policy tài khoản phòng (W3-08): RoomAccount chỉ thao tác
/// vé và station thuộc RoomId trong token, sai phòng → 403; bác sĩ chỉ ở phòng của phiên hôm nay. Một phiên mỗi phòng do
/// Identity bảo đảm (đăng nhập mới thu hồi phiên cũ, token phiên cũ bị từ chối).
/// </summary>
internal static class QueueEndpoints
{
    public static void MapQueueEndpoints(this RouteGroupBuilder scheduling)
    {
        scheduling.MapGet("/staff/rooms/{roomId:guid}/pending-results", async (Guid roomId, int? page, int? pageSize,
            ClaimsPrincipal user, [FromServices] IClsPendingResultReader reader, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(AppClaims.RoomId), out var room) || room == Guid.Empty || room != roomId)
                return (IResult)TypedResults.Forbid();
            var number = page ?? 1; var size = pageSize ?? 50;
            if (number < 1 || size is < 1 or > 100 || number > int.MaxValue / size)
                return Problem(QueueResult.InvalidInput);
            return TypedResults.Ok(await reader.ListAsync(roomId, number, size, ct));
        }).RequireAuthorization(Policies.RoomAccountOnly)
            .WithSummary("Vé CLS khám xong, chưa phát hành kết quả của phòng (RoomAccount)")
            .Produces<ClsPendingResultPage>().ProducesProblem(400).ProducesProblem(403);
        var staff = scheduling.MapGroup("/staff").RequireAuthorization(Policies.AnyChannel);
        staff.AddEndpointFilter(async (context, next) =>
        {
            var user = context.HttpContext.User;
            var audit = context.HttpContext.RequestServices.GetService<RoomAuditRequest>();
            if (audit is not null && user.IsInRole(Roles.RoomAccount))
            {
                audit.IsRoomAccount = true;
                Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var actor);
                Guid.TryParse(user.FindFirstValue(AppClaims.RoomId), out var room);
                audit.UserId = actor; audit.RoomId = room;
                audit.SessionToken = user.FindFirstValue("room_session");
                audit.ClientIpAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();
            }
            try { return await next(context); }
            catch (RoomAuditDeniedException) { return TypedResults.Forbid(); }
            // Clinical từ chối chuyển mục CLS (đợt hủy, lượt khám đã đóng...): UoW đã rollback cả vé.
            catch (Clinical.Contracts.ClinicalException e) { return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: e.Message,
                extensions: new Dictionary<string, object?> { ["code"] = e.Code.ToString() }); }
        });
        staff.MapPost("/rooms/{roomId:guid}/call-next", CallNextAsync)
            .WithSummary("Gọi người tiếp theo của phòng (Doctor, Nurse, RoomAccount)")
            .Produces<QueueTicketResponse>().Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        staff.MapPost("/tickets/{ticketId:guid}/start", (Guid ticketId, ClaimsPrincipal user, [FromServices] QueueService queue,
                [FromServices] IQueueStore store, CancellationToken ct) => TransitionAsync(ticketId, TicketTransition.Start, user, queue, store, ct))
            .WithSummary("Bắt đầu phục vụ vé đã gọi")
            .Produces<QueueTicketResponse>().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        staff.MapPost("/tickets/{ticketId:guid}/finish", (Guid ticketId, ClaimsPrincipal user, [FromServices] QueueService queue,
                [FromServices] IQueueStore store, CancellationToken ct) => TransitionAsync(ticketId, TicketTransition.Finish, user, queue, store, ct))
            .WithSummary("Khám xong: giải phóng bệnh nhân và vị trí")
            .Produces<QueueTicketResponse>().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        staff.MapPost("/tickets/{ticketId:guid}/requeue", (Guid ticketId, ClaimsPrincipal user, [FromServices] QueueService queue,
                [FromServices] IQueueStore store, CancellationToken ct) => TransitionAsync(ticketId, TicketTransition.Requeue, user, queue, store, ct))
            .WithSummary("Gọi không có mặt: trả vé về hàng chờ, giữ số gốc")
            .Produces<QueueTicketResponse>().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> CallNextAsync(Guid roomId, ClaimsPrincipal user, HttpRequest request,
        [FromServices] QueueService queue, [FromServices] IQueueStore store, CancellationToken ct)
    {
        if (!TryGetActor(user, out var userId))
            return TypedResults.Forbid();
        if (await AllowedRoomAsync(user, store, ct) is { } allowed && allowed != roomId)
            return TypedResults.Forbid();

        Guid? stationId = null;
        if (request.HasJsonContentType())
        {
            try
            {
                stationId = (await request.ReadFromJsonAsync<CallNextBody>(ct))?.StationId;
            }
            catch (System.Text.Json.JsonException)
            {
                return Problem(QueueResult.InvalidInput);
            }
        }

        var outcome = await queue.CallNextAsync(new CallNext(roomId, stationId, userId), ct);
        return outcome.Result == QueueResult.QueueEmpty ? TypedResults.NoContent() : ToResult(outcome);
    }

    private static async Task<IResult> TransitionAsync(Guid ticketId, TicketTransition transition, ClaimsPrincipal user,
        QueueService queue, IQueueStore store, CancellationToken ct)
    {
        if (!TryGetActor(user, out var userId))
            return TypedResults.Forbid();
        var room = await AllowedRoomAsync(user, store, ct);
        if (room == Guid.Empty)
            return TypedResults.Forbid();
        var command = new TicketCommand(ticketId, userId, room);
        return ToResult(transition switch
        {
            TicketTransition.Start => await queue.StartAsync(command, ct),
            TicketTransition.Finish => await queue.FinishAsync(command, ct),
            _ => await queue.RequeueAsync(command, ct),
        });
    }

    // Kênh Staff và vai trò được gọi số. Tiếp tân, admin không thao tác hàng đợi phòng.
    private static bool TryGetActor(ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        return user.HasClaim(AppClaims.Channel, Channels.Staff)
            && (user.IsInRole(Roles.Doctor) || user.IsInRole(Roles.Nurse) || user.IsInRole(Roles.RoomAccount))
            && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out userId)
            && userId != Guid.Empty;
    }

    /// <summary>
    /// Phòng người gọi được thao tác: RoomAccount theo claim, bác sĩ theo phiên hôm nay, điều dưỡng không giới hạn (null).
    /// Guid.Empty: không có phòng hợp lệ.
    /// </summary>
    private static async Task<Guid?> AllowedRoomAsync(ClaimsPrincipal user, IQueueStore store, CancellationToken ct)
    {
        if (user.IsInRole(Roles.RoomAccount))
            return Guid.TryParse(user.FindFirstValue(AppClaims.RoomId), out var room) ? room : Guid.Empty;
        if (user.IsInRole(Roles.Doctor))
            return Guid.TryParse(user.FindFirstValue(AppClaims.DoctorId), out var doctor)
                ? await store.GetDoctorRoomTodayAsync(doctor, ct) ?? Guid.Empty : Guid.Empty;
        return null;
    }

    private static IResult ToResult(QueueOutcome outcome) => outcome switch
    {
        { Result: QueueResult.Called, Ticket: { } t } => TypedResults.Ok(new QueueTicketResponse(t.QueueTicketId, t.TicketNumber,
            t.SequenceNumber, t.TicketType, t.QueueStatus, t.TargetRoomId, t.StationId, t.CalledAtUtc, outcome.AlreadyCalled)),
        { Result: QueueResult.WrongRoom } => TypedResults.Forbid(),
        _ => Problem(outcome.Result),
    };

    private static IResult Problem(QueueResult result) => TypedResults.Problem(statusCode: result switch
    {
        QueueResult.InvalidInput => 400,
        QueueResult.StationNotFound or QueueResult.TicketNotFound => 404,
        _ => 409,
    }, title: "Không thể xử lý hàng đợi.", extensions: new Dictionary<string, object?> { ["code"] = result.ToString() });

    private sealed record CallNextBody(Guid? StationId);

    /// <param name="AlreadyCalled">Lệnh lặp: station đã có vé này (bấm gọi hai lần) hoặc vé đã ở trạng thái đích.</param>
    private sealed record QueueTicketResponse(Guid QueueTicketId, string TicketNumber, int SequenceNumber, string TicketType,
        string QueueStatus, Guid TargetRoomId, Guid? StationId, DateTime? CalledAtUtc, bool AlreadyCalled);
}
