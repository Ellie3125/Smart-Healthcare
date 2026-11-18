using System.Text.Json;
using Clinical.Application.Results;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;
using Clinical.Contracts;

namespace Clinical.Infrastructure.Persistence.Stores;

public sealed class EfClsLifecycleStore(ClinicalDbContext db, AuditLogWriter audit) : IClsLifecycleStore
{
    public void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("CLS lifecycle requires the caller's IUnitOfWork with ClinicalDbContext.");
    }
    public async Task<ClsItemContext?> LockAsync(Guid itemId, CancellationToken ct)
    {
        RequireTransaction();
        var encounterId = await db.Set<ServiceOrderItem>().Where(x => x.Id == itemId)
            .Join(db.Set<ServiceOrder>(), i => i.ServiceOrderId, o => o.Id, (i, o) => o.EncounterId).SingleOrDefaultAsync(ct);
        if (encounterId == Guid.Empty) return null;
        var encounter = await db.Set<Encounter>().FromSql($"SELECT * FROM clinical.Encounter WITH (UPDLOCK,HOLDLOCK) WHERE Id={encounterId}").SingleAsync(ct);
        var item = await db.Set<ServiceOrderItem>().FromSql($"SELECT * FROM clinical.ServiceOrderItem WITH (UPDLOCK,HOLDLOCK) WHERE Id={itemId}").SingleAsync(ct);
        var order = await db.Set<ServiceOrder>().SingleAsync(x => x.Id == item.ServiceOrderId, ct);
        return new(encounter, order, item);
    }
    public async Task SaveAsync(ClsItemContext context, bool finish, ClsServiceCommand command, DateTime nowUtc, CancellationToken ct)
    {
        if (finish && !await db.Set<CLSResult>().AnyAsync(x => x.ServiceOrderItemId == context.Item.Id, ct))
            db.Add(new CLSResult { Id = Guid.CreateVersion7(), ServiceOrderItemId = context.Item.Id, ResultStatus = ResultStatus.Draft,
                ResultSummary = "", DetailedFindings = "", PerformedAtUtc = nowUtc });
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), PerformedByUserId = command.PerformedByUserId, OperationId = command.OperationId,
            TableName = "clinical.ServiceOrderItem", RecordId = context.Item.Id, ActionType = ActionType.UPDATE,
            ChangedColumns = finish ? "ItemStatus,CompletedAtUtc,CLSResult" : "ItemStatus",
            NewValuesJson = JsonSerializer.Serialize(new { context.Order.EncounterId, command.RoomId, ItemStatus = context.Item.ItemStatus.ToString() }), TimestampUtc = nowUtc }, ct);
    }
}
