using Clinical.Application.StaffLists;
using Clinical.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinical.Infrastructure.Persistence.Stores;

public sealed class EfClinicalStaffListReader(ClinicalDbContext db) : IClinicalStaffListReader
{
    public async Task<IReadOnlyList<NurseEncounterContext>> NurseContextsAsync(IReadOnlyList<Guid> encounterIds, Guid roomId, CancellationToken ct)
    {
        var rows = await db.Set<Encounter>().AsNoTracking().Where(e => encounterIds.Contains(e.Id) && e.OriginalRoomId == roomId)
            .Select(e => new { e.Id, e.PatientId, e.OriginalDoctorId, e.SpecialtySnapshot, e.Status, e.RowVersion,
                LastVitals = db.Set<VitalSign>().Where(v => v.EncounterId == e.Id).Max(v => (DateTime?)v.RecordedAtUtc) }).ToListAsync(ct);
        return rows.Select(e => new NurseEncounterContext(e.Id, e.PatientId, e.OriginalDoctorId, e.SpecialtySnapshot,
            e.Status.ToString(), Convert.ToBase64String(e.RowVersion), e.LastVitals)).ToArray();
    }

    public async Task<StaffListPage<DoctorRecordItem>> DoctorRecordsAsync(Guid doctorId, Guid? encounterId, int page, int pageSize, CancellationToken ct)
    {
        var rows = await (from encounter in db.Set<Encounter>().AsNoTracking()
                          join record in db.Set<EncounterClinicalRecord>().AsNoTracking() on encounter.Id equals record.EncounterId
                          where encounter.OriginalDoctorId == doctorId && (!encounterId.HasValue || encounter.Id == encounterId)
                          orderby encounter.CheckedInAtUtc descending, encounter.Id
                          select new { encounter.Id, encounter.PatientId, encounter.CheckedInAtUtc, record.Status,
                              Diagnosis = db.Set<EncounterDiagnosis>().Where(d => d.EncounterId == encounter.Id)
                                  .OrderByDescending(d => d.IsPrimary).ThenBy(d => d.Id).Select(d => d.DiagnosisName).FirstOrDefault() })
            .Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync(ct);
        return new(rows.Take(pageSize).Select(e => new DoctorRecordItem(e.Id, e.PatientId, "",
            DateOnly.FromDateTime(e.CheckedInAtUtc.AddHours(7)), e.Diagnosis ?? "", e.Status.ToString())).ToArray(),
            page, pageSize, rows.Count > pageSize);
    }
}
