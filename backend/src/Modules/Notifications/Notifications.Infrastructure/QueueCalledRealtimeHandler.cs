using IdentityPatients.Contracts;
using Notifications.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Notifications.Infrastructure;

public sealed class QueueCalledRealtimeHandler(IUnitOfWork uow, IProcessedEventStore processed, IRealtimeNotifier realtime,
    IIdentityPatientsModule identity, IPatientNotificationWriter writer)
    : IIntegrationEventHandler<QueueCalled>
{
    public async Task HandleAsync(QueueCalled e, CancellationToken ct)
    {
        var recipients = await uow.ExecuteAsync<IReadOnlyList<Guid>?>(async t =>
        {
            if (!await processed.TryMarkAsync(e.EventId, "Notifications.QueueCalled.Realtime.v1", t)) return null;
            await writer.SaveAsync(new(e.EventId, e.PatientId, PatientNotificationKind.TicketCalled, e.QueueTicketId), t);
            return await identity.GetPatientAppUserIdsAsync(e.PatientId, t);
        }, ct);
        if (recipients is null) return;
        // Best effort after commit: clients reload REST on reconnect if a crash or Redis outage loses this signal.
        var staff = new { e.EventId, e.RoomId, e.QueueTicketId, e.StationId, e.TicketNumber, CalledAtUtc = e.OccurredAtUtc };
        var patient = new { e.EventId, e.RoomId, e.QueueTicketId, e.TicketNumber, CalledAtUtc = e.OccurredAtUtc };
        await Task.WhenAll(recipients.Distinct().Select(id => realtime.ToPatientUserAsync(id, RealtimeEvents.QueueCalled, patient, ct)).Concat([
            realtime.ToStaffUserAsync(e.PerformedByUserId, RealtimeEvents.QueueUpdated, staff, ct),
            realtime.ToStaffRoomAsync(e.RoomId, RealtimeEvents.QueueUpdated, staff, ct),
            realtime.ToPublicDisplayAsync(e.RoomId, RealtimeEvents.DisplayBoardUpdated, new { e.TicketNumber, e.SequenceNumber }, ct)]));
    }
}
