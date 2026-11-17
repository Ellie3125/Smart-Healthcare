using Notifications.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Notifications.Infrastructure.PatientNotifications;

public sealed class AppointmentNotificationHandler(IUnitOfWork uow, IProcessedEventStore processed, IPatientNotificationWriter writer)
    : IIntegrationEventHandler<AppointmentConfirmed>, IIntegrationEventHandler<AppointmentCancelled>
{
    public Task HandleAsync(AppointmentConfirmed e, CancellationToken ct) => Save(e.EventId, e.PatientId,
        PatientNotificationKind.AppointmentConfirmed, e.AppointmentId, "Notifications.AppointmentConfirmed.v1", ct);
    public Task HandleAsync(AppointmentCancelled e, CancellationToken ct) => Save(e.EventId, e.PatientId,
        PatientNotificationKind.AppointmentCancelled, e.AppointmentId, "Notifications.AppointmentCancelled.v1", ct);
    private Task Save(Guid eventId, Guid patientId, PatientNotificationKind kind, Guid targetId, string handler, CancellationToken ct) =>
        uow.ExecuteAsync(async t =>
        {
            if (!await processed.TryMarkAsync(eventId, handler, t)) return;
            await writer.SaveAsync(new(eventId, patientId, kind, targetId), t);
        }, ct);
}
