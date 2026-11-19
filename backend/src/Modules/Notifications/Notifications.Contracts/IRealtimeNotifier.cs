namespace Notifications.Contracts;

/// <summary>
/// Đẩy sự kiện thời gian thực tới web nhân viên, web bệnh nhân và màn hình gọi số qua SignalR (W4-01, Giải pháp §8.6).
/// Gửi theo kiểu cố gắng: Redis hoặc kết nối lỗi thì ghi log và bỏ qua, không làm hỏng nghiệp vụ đã commit.
/// Client nhận sự kiện rồi tải lại qua API; payload chỉ mang ID và trạng thái, không PII trên màn hình công khai.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Mọi kết nối /hubs/staff của một tài khoản nhân viên (AppUser).</summary>
    Task ToStaffUserAsync(Guid userId, string eventName, object payload, CancellationToken ct);

    /// <summary>Kết nối /hubs/staff của tài khoản phòng có RoomId này.</summary>
    Task ToStaffRoomAsync(Guid roomId, string eventName, object payload, CancellationToken ct);

    /// <summary>Mọi kết nối /hubs/patient của một AppUser bệnh nhân (hoặc người đại diện).</summary>
    Task ToPatientUserAsync(Guid appUserId, string eventName, object payload, CancellationToken ct);

    /// <summary>Màn hình gọi số của phòng (/hubs/public-display?roomId=) và màn hình xem mọi phòng.</summary>
    Task ToPublicDisplayAsync(Guid roomId, string eventName, object payload, CancellationToken ct);
}

/// <summary>Tên sự kiện client đang lắng nghe (frontend/web, frontend/patient-web).</summary>
public static class RealtimeEvents
{
    public const string NotificationReceived = "NotificationReceived";
    public const string QueueUpdated = "QueueUpdated";
    public const string SessionReplaced = "SessionReplaced";
    public const string JourneyUpdated = "JourneyUpdated";
    public const string QueueCalled = "QueueCalled";
    public const string AppointmentsUpdated = "AppointmentsUpdated";
    public const string DisplayBoardUpdated = "DisplayBoardUpdated";
}
