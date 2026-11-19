using IdentityPatients.Contracts;
using Notifications.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Notifications.Infrastructure.PatientNotifications;

public sealed class AppointmentNotificationHandler(IUnitOfWork uow, IProcessedEventStore processed, IPatientNotificationWriter writer,
    IIdentityPatientsModule identity, IRealtimeNotifier realtime)
    : IIntegrationEventHandler<AppointmentConfirmed>, IIntegrationEventHandler<AppointmentCancelled>
{
    public Task HandleAsync(AppointmentConfirmed e, CancellationToken ct) => Save(e.EventId, e.PatientId,
        PatientNotificationKind.AppointmentConfirmed, e.AppointmentId, "Notifications.AppointmentConfirmed.v1", ct);
    public Task HandleAsync(AppointmentCancelled e, CancellationToken ct) => Save(e.EventId, e.PatientId,
        PatientNotificationKind.AppointmentCancelled, e.AppointmentId, "Notifications.AppointmentCancelled.v1", ct);

    private async Task Save(Guid eventId, Guid patientId, PatientNotificationKind kind, Guid targetId, string handler, CancellationToken ct)
    {
        var recipients = await uow.ExecuteAsync<IReadOnlyList<Guid>?>(async t =>
        {
            if (!await processed.TryMarkAsync(eventId, handler, t)) return null;
            await writer.SaveAsync(new(eventId, patientId, kind, targetId), t);
            return await identity.GetPatientAppUserIdsAsync(patientId, t);
        }, ct);
        if (recipients is null) return;
        // Cố gắng sau commit: trang lịch tải lại qua REST; mất tín hiệu thì lần mở trang sau vẫn đúng.
        var payload = new { EventId = eventId, AppointmentId = targetId, Kind = kind.ToString() };
        await Task.WhenAll(recipients.Distinct().Select(id => realtime.ToPatientUserAsync(id, RealtimeEvents.AppointmentsUpdated, payload, ct)));
    }
}
