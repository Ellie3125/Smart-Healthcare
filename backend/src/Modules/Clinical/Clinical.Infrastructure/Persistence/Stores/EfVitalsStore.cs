using System.Text.Json;
using Clinical.Application.Vitals;
using Clinical.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Clinical.Infrastructure.Persistence.Stores;

public sealed class EfVitalsStore(ClinicalDbContext db, AuditLogWriter audit) : IVitalsStore
{
    public async Task<Encounter?> LockEncounterAsync(Guid encounterId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Recording vitals requires IUnitOfWork with ClinicalDbContext.");
        return await db.Set<Encounter>().FromSql($"SELECT * FROM clinical.Encounter WITH (UPDLOCK,HOLDLOCK) WHERE Id={encounterId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
    }

    public async Task AppendAsync(VitalSign vitalSign, Guid actorUserId, CancellationToken ct)
    {
        db.Add(vitalSign);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), PerformedByUserId = actorUserId,
            TableName = "clinical.VitalSign", RecordId = vitalSign.Id, ActionType = ActionType.INSERT,
            ChangedColumns = "EncounterId,WeightKg,HeightCm,SystolicBP,DiastolicBP,PulseRate,BodyTemperature,SpO2,RespiratoryRate,NurseNote,RecordedByStaffId,RecordedAtUtc",
            // Keep clinical measurements and free-text notes out of the infrastructure audit log.
            NewValuesJson = JsonSerializer.Serialize(new { vitalSign.EncounterId, vitalSign.RecordedByStaffId }),
            TimestampUtc = vitalSign.RecordedAtUtc }, ct);
    }
}
