namespace Notifications.Contracts;

public enum PatientNotificationKind { AppointmentConfirmed, AppointmentCancelled, DoctorChanged, ClsOrdered, ResultsAvailable, TicketCalled, ReturnTicketActivated }
public sealed record CreatePatientNotification(Guid SourceEventId, Guid PatientId, PatientNotificationKind Kind, Guid TargetEntityId);
public interface IPatientNotificationWriter
{
    Task<Guid> SaveAsync(CreatePatientNotification command, CancellationToken ct);
}
