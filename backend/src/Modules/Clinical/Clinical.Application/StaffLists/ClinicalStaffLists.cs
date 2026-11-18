namespace Clinical.Application.StaffLists;

public sealed record StaffListPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);
public sealed record NurseEncounterContext(Guid EncounterId, Guid PatientId, Guid DoctorId, string SpecialtyName,
    string EncounterStatus, string Version, DateTime? LastVitalsAtUtc);
public sealed record NurseQueueItem(Guid EncounterId, Guid QueueTicketId, string TicketNumber, string PatientName,
    Guid DoctorId, string SpecialtyName, string Status, string EncounterStatus, string Version, DateTime? LastVitalsAtUtc, string? DisplayNumber = null);
public sealed record DoctorRecordItem(Guid Id, Guid PatientId, string PatientName, DateOnly ServiceDate,
    string Diagnosis, string Status);

public interface IClinicalStaffListReader
{
    Task<IReadOnlyList<NurseEncounterContext>> NurseContextsAsync(IReadOnlyList<Guid> encounterIds, Guid roomId, CancellationToken ct);
    Task<StaffListPage<DoctorRecordItem>> DoctorRecordsAsync(Guid doctorId, Guid? encounterId, int page, int pageSize, CancellationToken ct);
}
