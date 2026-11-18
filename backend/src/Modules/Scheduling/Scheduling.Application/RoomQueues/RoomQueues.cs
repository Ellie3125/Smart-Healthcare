namespace Scheduling.Application.RoomQueues;

public sealed record RoomQueueEntry(Guid QueueTicketId, string TicketNumber, int SequenceNumber, string TicketType,
    string QueueStatus, Guid? TargetStationId, DateTime? CalledAtUtc, Guid? EncounterId = null, Guid? ServiceOrderItemId = null, string PatientName = "");
public sealed record RoomQueuePage(DateOnly ServiceDate, int Page, int PageSize, bool HasMore,
    IReadOnlyList<RoomQueueEntry> Items);
public sealed record PublicQueueNumber(string TicketNumber, int SequenceNumber);
public sealed record PublicRoomDisplay(IReadOnlyList<PublicQueueNumber> Tickets);
public sealed record StaffRoomOption(Guid RoomId, string RoomName);

public interface IRoomQueueReader
{
    /// <summary>doctorId có giá trị thì kiểm phân công bác sĩ/phòng hôm nay; null kết quả là không được xem phòng.</summary>
    Task<RoomQueuePage?> GetStaffAsync(Guid roomId, Guid? doctorId, int page, int pageSize, CancellationToken ct);
    Task<PublicRoomDisplay> GetPublicAsync(Guid roomId, CancellationToken ct);

    /// <summary>Phòng có phiên khám hôm nay (giờ DB, GMT+7); doctorId có giá trị thì chỉ phòng bác sĩ đó được phân công.</summary>
    Task<IReadOnlyList<Guid>> GetTodayRoomIdsAsync(Guid? doctorId, CancellationToken ct) => throw new NotSupportedException();
}
