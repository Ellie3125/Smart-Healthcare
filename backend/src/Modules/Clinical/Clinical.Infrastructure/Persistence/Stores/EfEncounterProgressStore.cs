using System.Text.Json;
using Clinical.Application.Encounters;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Clinical.Infrastructure.Persistence.Stores;

public sealed class EfEncounterProgressStore(ClinicalDbContext db, AuditLogWriter audit) : IEncounterProgressStore
{
    public async Task<Encounter?> LockAsync(Guid encounterId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Tiến trình lượt khám cần ClinicalDbContext trong IUnitOfWork của bên gọi.");
        return await db.Set<Encounter>().FromSql($"SELECT * FROM clinical.Encounter WITH (UPDLOCK,HOLDLOCK) WHERE Id={encounterId}")
            .SingleOrDefaultAsync(ct);
    }

    public Task<bool> IsRecordPublishedAsync(Guid encounterId, CancellationToken ct) =>
        db.Set<EncounterClinicalRecord>().AnyAsync(r => r.EncounterId == encounterId && r.Status == EncounterClinicalRecordStatus.Published, ct);

    public async Task SaveAsync(Encounter encounter, EncounterStatus from, Guid performedByUserId, DateTime nowUtc, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditLog
        {
            Id = Guid.CreateVersion7(), PerformedByUserId = performedByUserId, TableName = "clinical.Encounter", RecordId = encounter.Id,
            ActionType = ActionType.UPDATE, ChangedColumns = encounter.CompletedAtUtc is null ? "Status" : "Status,CompletedAtUtc",
            OldValuesJson = JsonSerializer.Serialize(new { Status = from.ToString() }),
            NewValuesJson = JsonSerializer.Serialize(new { Status = encounter.Status.ToString(), encounter.CompletedAtUtc }), TimestampUtc = nowUtc,
        }, ct);
    }
}
