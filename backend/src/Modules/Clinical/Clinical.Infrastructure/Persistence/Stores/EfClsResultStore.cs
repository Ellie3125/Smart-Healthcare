using System.Text.Json;
using Clinical.Application.Results;
using Clinical.Domain.Entities;
using Clinical.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Clinical.Infrastructure.Persistence.Stores;

public sealed class EfClsResultStore(ClinicalDbContext db, IClsLifecycleStore lifecycle, AuditLogWriter audit) : IClsResultStore
{
    public async Task<ClsResultContext?> LoadAsync(Guid itemId, bool forUpdate, CancellationToken ct)
    {
        ClsItemContext? context;
        if (forUpdate) context = await lifecycle.LockAsync(itemId, ct);
        else
        {
            context = await (from i in db.Set<ServiceOrderItem>().AsNoTracking()
                join o in db.Set<ServiceOrder>().AsNoTracking() on i.ServiceOrderId equals o.Id
                join e in db.Set<Encounter>().AsNoTracking() on o.EncounterId equals e.Id
                where i.Id == itemId select new ClsItemContext(e, o, i)).SingleOrDefaultAsync(ct);
        }
        if (context is null) return null;
        var result = forUpdate
            ? await db.Set<CLSResult>().FromSql($"SELECT * FROM clinical.CLSResult WITH (UPDLOCK,HOLDLOCK) WHERE ServiceOrderItemId={itemId}").SingleOrDefaultAsync(ct)
            : await db.Set<CLSResult>().AsNoTracking().SingleOrDefaultAsync(x => x.ServiceOrderItemId == itemId, ct);
        return result is null ? null : new(context, result);
    }
    public async Task SaveAsync(ClsResultContext context, Guid roomId, Guid actorId, DateTime nowUtc, bool publish, CancellationToken ct)
    {
        lifecycle.RequireTransaction();
        var r = context.Result;
        if (publish) db.Add(new CLSResultRevision { Id = Guid.CreateVersion7(), CLSResultId = r.Id, RevisionNumber = 1,
            Kind = ClsResultRevisionKind.Published, ResultSummary = r.ResultSummary, DetailedFindings = r.DetailedFindings,
            ChangedByAccountId = actorId, ChangedAtUtc = nowUtc });
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), TableName = "clinical.CLSResult", RecordId = r.Id,
            PerformedByUserId = actorId, ActionType = publish ? ActionType.PUBLISH : ActionType.UPDATE, TimestampUtc = nowUtc,
            ChangedColumns = publish ? "ResultStatus,PublishedAtUtc,ApprovedByStaffId,Revision,ItemStatus" : "ResultSummary,DetailedFindings,ApprovedByStaffId",
            NewValuesJson = JsonSerializer.Serialize(new { context.Clinical.Order.EncounterId, roomId, r.ApprovedByStaffId, ResultStatus = r.ResultStatus.ToString() }) }, ct);
    }
}
