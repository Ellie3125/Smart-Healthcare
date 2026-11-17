namespace Scheduling.Application.SessionAdministration;

public enum SessionAdminError { InvalidInput, DayNotFound, DoctorUnavailable, AlreadyHasSession, SessionStarted, AlreadyCheckedIn, InvalidState, RoomNotFound }
public sealed class SessionAdminException(SessionAdminError error) : Exception(error.ToString()) { public SessionAdminError Error { get; } = error; }
public sealed record SessionAdminView(Guid Id, Guid DoctorId, Guid AssignedRoomId, DateOnly ServiceDate, int HeldCount,
    int AdmittedCount, int LockedCancelledCount, bool IsOpen, string SessionStatus);
public sealed record RoomDisabledView(Guid RoomId, int StationsDisabled, int MappingsDisabled);
public sealed record StaffCallItem(Guid Id, Guid AppointmentId, Guid PatientId, Guid? OldDoctorId, Guid? NewDoctorId,
    DateOnly ServiceDate, string Reason, DateTime OccurredAtUtc);
public sealed record StaffCallPage(IReadOnlyList<StaffCallItem> Items, int Page, int PageSize, bool HasMore, bool ContactsAvailable);
public interface ISessionAdministration
{
    Task<SessionAdminView> ReplaceDoctorAsync(Guid doctorId, DateOnly date, Guid replacementDoctorId, Guid userId, string reason, CancellationToken ct);
    Task<SessionAdminView> CancelSessionAsync(Guid doctorId, DateOnly date, Guid userId, string reason, CancellationToken ct);
    Task<RoomDisabledView> DisableRoomAsync(Guid roomId, Guid userId, string reason, CancellationToken ct);
    Task<StaffCallPage> GetCallListAsync(int page, int pageSize, CancellationToken ct);
}
