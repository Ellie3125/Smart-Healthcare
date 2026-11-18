namespace Scheduling.Application.RoomQueues;

public sealed record RoomQueueEntry(Guid QueueTicketId, string TicketNumber, int SequenceNumber, string TicketType,
    string QueueStatus, Guid? TargetStationId, DateTime? CalledAtUtc, Guid? EncounterId = null, Guid? ServiceOrderItemId = null, string PatientName = "");
public sealed record RoomQueuePage(DateOnly ServiceDate, int Page, int PageSize, bool HasMore,
    IReadOnlyList<RoomQueueEntry> Items);
public sealed record PublicQueueNumber(string TicketNumber, int SequenceNumber);
public sealed record PublicRoomDisplay(IReadOnlyList<PublicQueueNumber> Tickets);

public interface IRoomQueueReader
{
    /// <summary>doctorId có giá trị thì kiểm phân công bác sĩ/phòng hôm nay; null kết quả là không được xem phòng.</summary>
    Task<RoomQueuePage?> GetStaffAsync(Guid roomId, Guid? doctorId, int page, int pageSize, CancellationToken ct);
    Task<PublicRoomDisplay> GetPublicAsync(Guid roomId, CancellationToken ct);
}
