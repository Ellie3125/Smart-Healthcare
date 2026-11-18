namespace Scheduling.Contracts;

public sealed record ClsTicketContext(Guid TicketId, Guid EncounterId, Guid ServiceOrderItemId, Guid RoomId, string QueueStatus, string TicketNumber);

/// <summary>Read-only authoritative CLS ticket context; missing/non-CLS tickets return null.</summary>
public interface IClsTicketReader
{
    Task<ClsTicketContext?> GetByIdAsync(Guid ticketId, CancellationToken ct);
    Task<ClsTicketContext?> GetByItemAsync(Guid orderItemId, CancellationToken ct);
    Task<IReadOnlyList<ClsTicketContext>> GetByEncounterRoomAsync(Guid encounterId, Guid roomId, CancellationToken ct) =>
        throw new NotSupportedException();
}
