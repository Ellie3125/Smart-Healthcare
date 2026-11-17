using Clinical.Application;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Clinical.Infrastructure.Persistence.Stores;

internal sealed class EfEncounterStore(ClinicalDbContext db) : IEncounterStore
{
    private static readonly EncounterStatus[] ActiveStatuses =
    [
        EncounterStatus.InProgress,
        EncounterStatus.WaitingCLS,
        EncounterStatus.ReturningDoctor
    ];

    public Task<Encounter?> GetByIdAsync(Guid encounterId, CancellationToken ct) =>
        db.Set<Encounter>().FirstOrDefaultAsync(e => e.Id == encounterId, ct);

    public Task<Encounter?> GetActiveByPatientIdAsync(Guid patientId, CancellationToken ct) =>
        db.Set<Encounter>()
            .Where(e => e.PatientId == patientId && ActiveStatuses.Contains(e.Status))
            .OrderByDescending(e => e.CheckedInAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Encounter>> GetByPatientIdAsync(Guid patientId, CancellationToken ct) =>
        await db.Set<Encounter>()
            .AsNoTracking()
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.CheckedInAtUtc)
            .ToListAsync(ct);

    public Task<EncounterClinicalRecord?> GetRecordByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        db.Set<EncounterClinicalRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.EncounterId == encounterId, ct);

    public async Task<IReadOnlyList<VitalSign>> GetVitalSignsByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        await db.Set<VitalSign>()
            .AsNoTracking()
            .Where(v => v.EncounterId == encounterId)
            .OrderByDescending(v => v.RecordedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EncounterDiagnosis>> GetDiagnosesByEncounterIdAsync(Guid encounterId, CancellationToken ct) =>
        await db.Set<EncounterDiagnosis>()
            .AsNoTracking()
            .Where(d => d.EncounterId == encounterId)
            .OrderByDescending(d => d.IsPrimary)
            .ThenBy(d => d.DiagnosedAtUtc)
            .ToListAsync(ct);

    public void AddEncounter(Encounter encounter) => db.Set<Encounter>().Add(encounter);

    public void AddClinicalRecord(EncounterClinicalRecord record) => db.Set<EncounterClinicalRecord>().Add(record);

    public void AddVitalSign(VitalSign vitalSign) => db.Set<VitalSign>().Add(vitalSign);

    public void AddDiagnosis(EncounterDiagnosis diagnosis) => db.Set<EncounterDiagnosis>().Add(diagnosis);

    public void RemoveDiagnosis(EncounterDiagnosis diagnosis) => db.Set<EncounterDiagnosis>().Remove(diagnosis);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

