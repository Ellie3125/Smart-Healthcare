namespace Scheduling.Contracts;

public sealed record CoordinationExceptionView(Guid QueueTicketId, Guid EncounterId, Guid RoomId, Guid OriginalDoctorId,
    string QueueStatus, string Reason, DateTime QueuedAtUtc, bool RequiresManualHandling);
public interface ICoordinationExceptionReader
{
    Task<IReadOnlyList<CoordinationExceptionView>> ListAsync(int take, CancellationToken ct);
}
