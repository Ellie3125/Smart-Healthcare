using System.Security.Claims;
using IdentityPatients.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Notifications.Contracts;

namespace Notifications.Api;

/// <summary>Web nhân viên. Nhóm theo tài khoản và theo phòng (tài khoản phòng). Chỉ nhận kết nối kênh Staff.</summary>
// Chặn vai trò ngoài nhân viên ngay ở bước authorize (trước khi nâng cấp WebSocket); kênh kiểm thêm khi kết nối.
[Authorize(Policy = Policies.AnyChannel,
    Roles = $"{Roles.Admin},{Roles.Receptionist},{Roles.Nurse},{Roles.Doctor},{Roles.RoomAccount}")]
public sealed class StaffHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        if (!user.HasClaim(AppClaims.Channel, Channels.Staff) || RealtimeGroups.UserId(user) is not { } userId)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId));
        if (user.IsInRole(Roles.RoomAccount) && Guid.TryParse(user.FindFirstValue(AppClaims.RoomId), out var roomId))
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Room(roomId));
        if (user.IsInRole(Roles.Receptionist))
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Reception);
        await base.OnConnectedAsync();
    }
}

/// <summary>Web bệnh nhân: chỉ nhận sự kiện của chính AppUser đang đăng nhập.</summary>
[Authorize(Policy = Policies.PatientOnly)]
public sealed class PatientHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (RealtimeGroups.UserId(Context.User!) is not { } userId)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.User(userId));
        await base.OnConnectedAsync();
    }
}

/// <summary>Màn hình gọi số công khai: không đăng nhập, chỉ nhận số vé. Chọn phòng bằng query <c>roomId</c>.</summary>
[AllowAnonymous]
public sealed class PublicDisplayHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var room = Context.GetHttpContext()?.Request.Query["roomId"].ToString();
        await Groups.AddToGroupAsync(Context.ConnectionId,
            Guid.TryParse(room, out var roomId) ? RealtimeGroups.Display(roomId) : RealtimeGroups.AllDisplays);
        await base.OnConnectedAsync();
    }
}

internal static class RealtimeGroups
{
    public const string AllDisplays = "display:all";
    public const string Reception = "role:reception";
    public static string User(Guid id) => $"user:{id:N}";
    public static string Room(Guid id) => $"room:{id:N}";
    public static string Display(Guid roomId) => $"display:{roomId:N}";

    public static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var id) && id != Guid.Empty
            ? id : null;
}

/// <summary>Gửi qua IHubContext: Api gửi tới kết nối của mình, Redis backplane chuyển tới BE còn lại; Worker gửi qua Redis.</summary>
internal sealed class SignalRRealtimeNotifier(IHubContext<StaffHub> staff, IHubContext<PatientHub> patient,
    IHubContext<PublicDisplayHub> display, ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    // Redis chết thì lệnh gửi có thể chờ; giới hạn để không giữ request/job nghiệp vụ.
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(3);

    public Task ToStaffUserAsync(Guid userId, string eventName, object payload, CancellationToken ct) =>
        SendAsync(t => staff.Clients.Group(RealtimeGroups.User(userId)).SendAsync(eventName, payload, t), eventName, ct);

    public Task ToStaffRoomAsync(Guid roomId, string eventName, object payload, CancellationToken ct) =>
        SendAsync(t => staff.Clients.Group(RealtimeGroups.Room(roomId)).SendAsync(eventName, payload, t), eventName, ct);

    public Task ToReceptionAsync(string eventName, object payload, CancellationToken ct) =>
        SendAsync(t => staff.Clients.Group(RealtimeGroups.Reception).SendAsync(eventName, payload, t), eventName, ct);

    public Task ToPatientUserAsync(Guid appUserId, string eventName, object payload, CancellationToken ct) =>
        SendAsync(t => patient.Clients.Group(RealtimeGroups.User(appUserId)).SendAsync(eventName, payload, t), eventName, ct);

    public Task ToPublicDisplayAsync(Guid roomId, string eventName, object payload, CancellationToken ct) =>
        SendAsync(t => display.Clients.Groups(RealtimeGroups.Display(roomId), RealtimeGroups.AllDisplays)
            .SendAsync(eventName, payload, t), eventName, ct);

    private async Task SendAsync(Func<CancellationToken, Task> send, string eventName, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SendTimeout);
        try
        {
            await send(timeout.Token).WaitAsync(timeout.Token);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            // Client tự kết nối lại và tải lại qua API; mất một thông báo không làm sai dữ liệu.
            logger.LogWarning(e, "Không gửi được sự kiện SignalR {EventName}", eventName);
        }
    }
}
