using Notifications.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Notifications.Infrastructure;

public sealed class QueueCalledRealtimeHandler(IUnitOfWork uow, IProcessedEventStore processed, IRealtimeNotifier realtime)
    : IIntegrationEventHandler<QueueCalled>
{
    public async Task HandleAsync(QueueCalled e, CancellationToken ct)
    {
        if (!await uow.ExecuteAsync(t => processed.TryMarkAsync(e.EventId, "Notifications.QueueCalled.Realtime.v1", t), ct)) return;
        // Best effort after commit: clients reload REST on reconnect if a crash or Redis outage loses this signal.
        var staff = new { e.EventId, e.RoomId, e.QueueTicketId, e.StationId, e.TicketNumber, CalledAtUtc = e.OccurredAtUtc };
        await Task.WhenAll(
            realtime.ToStaffUserAsync(e.PerformedByUserId, RealtimeEvents.QueueUpdated, staff, ct),
            realtime.ToStaffRoomAsync(e.RoomId, RealtimeEvents.QueueUpdated, staff, ct),
            realtime.ToPublicDisplayAsync(e.RoomId, RealtimeEvents.DisplayBoardUpdated, new { e.TicketNumber, e.SequenceNumber }, ct));
    }
}
