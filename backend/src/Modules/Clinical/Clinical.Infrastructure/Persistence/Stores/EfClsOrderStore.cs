using System.Text.Json;
using Clinical.Application.Orders;
using Clinical.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Clinical.Infrastructure.Persistence.Stores;

public sealed class EfClsOrderStore(ClinicalDbContext db, AuditLogWriter audit) : IClsOrderStore
{
    public async Task<Encounter?> LockEncounterAsync(Guid encounterId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Ordering CLS requires ClinicalDbContext in IUnitOfWork.");
        return await db.Set<Encounter>().FromSql($"SELECT * FROM clinical.Encounter WITH (UPDLOCK,HOLDLOCK) WHERE Id={encounterId}").SingleOrDefaultAsync(ct);
    }
    public async Task<int> NextRoundAsync(Guid encounterId, CancellationToken ct) =>
        (await db.Set<ServiceOrder>().Where(x => x.EncounterId == encounterId).MaxAsync(x => (int?)x.OrderRoundNumber, ct) ?? 0) + 1;
    public async Task AddAsync(ServiceOrder order, IReadOnlyList<ServiceOrderItem> items, CancellationToken ct)
    {
        db.Add(order); db.AddRange(items); await db.SaveChangesAsync(ct);
    }
    public async Task ConfirmAsync(ServiceOrder order, IReadOnlyList<ServiceOrderItem> items, Encounter encounter, Guid userId, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), PerformedByUserId = userId, TableName = "clinical.ServiceOrder", RecordId = order.Id,
            ActionType = ActionType.INSERT, ChangedColumns = "ServiceOrder,ServiceOrderItem,Encounter.Status",
            NewValuesJson = JsonSerializer.Serialize(new { order.EncounterId, order.OrderRoundNumber, order.PrescribedByDoctorId, ItemCount = items.Count }), TimestampUtc = order.OrderedAtUtc }, ct);
    }
}
