namespace Scheduling.Application.RoomQueues;

public sealed record RoomQueueEntry(Guid QueueTicketId, string TicketNumber, int SequenceNumber, string TicketType,
    string QueueStatus, Guid? TargetStationId, DateTime? CalledAtUtc, Guid? EncounterId = null, Guid? ServiceOrderItemId = null, string PatientName = "", string? DisplayNumber = null);
public sealed record RoomQueuePage(DateOnly ServiceDate, int Page, int PageSize, bool HasMore,
    IReadOnlyList<RoomQueueEntry> Items);
public sealed record PublicQueueNumber(string TicketNumber, int SequenceNumber, string? DisplayNumber = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string TicketType { get; init; } = "InitialConsultation";
}
public sealed record PublicRoomDisplay(IReadOnlyList<PublicQueueNumber> Tickets);
public sealed record StaffStationOption(Guid StationId, string StationCode, string StationName);
public sealed record StaffRoomOption(Guid RoomId, string RoomName, IReadOnlyList<StaffStationOption> Stations);
public sealed record RoomStation(Guid RoomId, StaffStationOption Station);

public interface IRoomQueueReader
{
    /// <summary>doctorId có giá trị thì kiểm phân công bác sĩ/phòng hôm nay; null kết quả là không được xem phòng.</summary>
    Task<RoomQueuePage?> GetStaffAsync(Guid roomId, Guid? doctorId, int page, int pageSize, CancellationToken ct);
    Task<PublicRoomDisplay> GetPublicAsync(Guid roomId, CancellationToken ct);

    /// <summary>Phòng có phiên khám hôm nay (giờ DB, GMT+7); doctorId có giá trị thì chỉ phòng bác sĩ đó được phân công.</summary>
    Task<IReadOnlyList<Guid>> GetTodayRoomIdsAsync(Guid? doctorId, CancellationToken ct) => throw new NotSupportedException();

    /// <summary>Vị trí phục vụ đang hoạt động của các phòng; phòng có nhiều vị trí phải gửi stationId khi gọi số.</summary>
    Task<IReadOnlyList<RoomStation>> GetActiveStationsAsync(IReadOnlyCollection<Guid> roomIds, CancellationToken ct) => throw new NotSupportedException();
}
