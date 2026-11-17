using System.Text.Json;
using Clinical.Application.Records;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Clinical.Infrastructure.Persistence.Stores;

public sealed class EfClinicalRecordStore(ClinicalDbContext db, AuditLogWriter audit) : IClinicalRecordStore
{
    public async Task<Encounter?> LockEncounterAsync(Guid encounterId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Clinical records require ClinicalDbContext in IUnitOfWork.");
        return await db.Set<Encounter>().FromSql($"SELECT * FROM clinical.Encounter WITH (UPDLOCK,HOLDLOCK) WHERE Id={encounterId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
    }

    public Task<EncounterClinicalRecord?> GetRecordAsync(Guid encounterId, CancellationToken ct) =>
        db.Set<EncounterClinicalRecord>().FromSql($"SELECT * FROM clinical.EncounterClinicalRecord WITH (UPDLOCK,HOLDLOCK) WHERE EncounterId={encounterId}")
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EncounterDiagnosis>> GetDiagnosesAsync(Guid encounterId, CancellationToken ct) =>
        await db.Set<EncounterDiagnosis>().Where(x => x.EncounterId == encounterId).OrderBy(x => x.Id).ToListAsync(ct);

    public async Task SaveAsync(EncounterClinicalRecord record, IReadOnlyList<EncounterDiagnosis> diagnoses, RecordAction action,
        string? reason, Guid actorUserId, CancellationToken ct)
    {
        if (action != RecordAction.Publish)
        {
            db.RemoveRange(await db.Set<EncounterDiagnosis>().Where(x => x.EncounterId == record.EncounterId).ToListAsync(ct));
            db.AddRange(diagnoses);
        }
        int? revisionNumber = null;
        if (action != RecordAction.Draft)
        {
            revisionNumber = (await db.Set<EncounterClinicalRecordRevision>().Where(x => x.ClinicalRecordId == record.Id)
                .MaxAsync(x => (int?)x.RevisionNumber, ct) ?? 0) + 1;
            // Legacy Published rows need an approved baseline, rather than inventing a prior version.
            if (action == RecordAction.Amend && revisionNumber == 1) throw new ClinicalRecordException(RecordError.HistoryUnavailable);
            db.Add(new EncounterClinicalRecordRevision { Id = Guid.CreateVersion7(), ClinicalRecordId = record.Id,
                RevisionNumber = revisionNumber.Value, Kind = action == RecordAction.Publish ? ClinicalRecordRevisionKind.Published : ClinicalRecordRevisionKind.Amended,
                ChiefComplaint = record.ChiefComplaint, Symptoms = record.Symptoms, MedicalHistory = record.MedicalHistory,
                ClinicalExaminationNotes = record.ClinicalExaminationNotes, InitialClinicalImpression = record.InitialClinicalImpression,
                PostCLSConclusion = record.PostCLSConclusion, TreatmentPlan = record.TreatmentPlan, FollowUpInstructions = record.FollowUpInstructions,
                DiagnosesJson = JsonSerializer.Serialize(diagnoses.Select(d => new EncounterDiagnosisDto(d.Id, d.EncounterId,
                    d.DiagnosisType.ToString(), d.ICD10Code, d.DiagnosisName, d.IsPrimary, d.ClinicalNotes, d.DiagnosedAtUtc))), Reason = reason,
                ChangedByDoctorId = record.UpdatedByDoctorId, ChangedAtUtc = record.LastUpdatedUtc });
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ClinicalRecordException(RecordError.VersionConflict); }
        await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), PerformedByUserId = actorUserId,
            TableName = "clinical.EncounterClinicalRecord", RecordId = record.Id,
            ActionType = action == RecordAction.Publish ? ActionType.PUBLISH : ActionType.UPDATE,
            ChangedColumns = action.ToString(), NewValuesJson = JsonSerializer.Serialize(new { record.EncounterId,
                record.UpdatedByDoctorId, Status = record.Status.ToString(), RevisionNumber = revisionNumber }), TimestampUtc = record.LastUpdatedUtc }, ct);
    }
}
