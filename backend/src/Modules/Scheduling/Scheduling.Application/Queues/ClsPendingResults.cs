namespace Scheduling.Application.Queues;

public sealed record ClsPendingResult(Guid QueueTicketId, string TicketNumber, Guid ServiceOrderItemId, Guid EncounterId, DateTime? CompletedAtUtc);
public sealed record ClsPendingResultPage(IReadOnlyList<ClsPendingResult> Items, bool HasMore);
public interface IClsPendingResultReader
{
    Task<ClsPendingResultPage> ListAsync(Guid roomId, int page, int pageSize, CancellationToken ct);
}
