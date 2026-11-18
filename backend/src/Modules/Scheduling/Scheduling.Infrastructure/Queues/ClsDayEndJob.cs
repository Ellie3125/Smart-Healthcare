using System.Text.Json;
using Clinical.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Scheduling.Infrastructure.Queues;

public sealed class ClsDayEndJob(SchedulingDbContext db, IClinicalModule clinical, IUnitOfWork uow, IDbClock clock,
    AuditLogWriter audit, IIntegrationEventPublisher publisher)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var cutoff = SqlQueueStore.StartOfVietnamDayUtc(await clock.GetUtcNowAsync(ct));
        var ids = await db.Set<QueueTicket>().AsNoTracking().Where(q => q.TicketType == TicketType.CLS && q.IssuedAtUtc < cutoff
            && (q.QueueStatus == QueueStatus.Waiting || q.QueueStatus == QueueStatus.Called))
            .OrderBy(q => q.IssuedAtUtc).ThenBy(q => q.Id).Select(q => q.Id).ToListAsync(ct);
        var cancelled = 0;
        foreach (var id in ids) if (await TryCancelAsync(id, ct)) cancelled++;
        return cancelled;
    }

    public Task<bool> TryCancelAsync(Guid ticketId, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Scheduling must participate in IUnitOfWork.");
        var now = await clock.GetUtcNowAsync(t); var cutoff = SqlQueueStore.StartOfVietnamDayUtc(now);
        var ticket = await db.Set<QueueTicket>().AsNoTracking().SingleOrDefaultAsync(q => q.Id == ticketId, t);
        if (!Eligible(ticket, cutoff)) return false;
        var encounter = await clinical.GetEncounterByIdAsync(ticket!.EncounterId, t);
        if (encounter is null || encounter.Id != ticket.EncounterId || encounter.PatientId == Guid.Empty) return false;
        await db.AppLockAsync(SqlQueueStore.PatientLock(encounter.PatientId), t);
        var allocation = await db.Set<ActiveStationAllocation>().AsNoTracking().SingleOrDefaultAsync(a => a.CurrentTicketId == ticketId, t);
        if (allocation is not null && allocation.CurrentPatientId != encounter.PatientId) return false;
        if ((allocation?.StationId ?? ticket.TargetStationId) is { } station) await db.AppLockAsync(SqlQueueStore.StationLock(station), t);
        ticket = await db.Set<QueueTicket>().FromSql($"SELECT * FROM scheduling.QueueTicket WITH (UPDLOCK,HOLDLOCK) WHERE Id={ticketId}")
            .AsNoTracking().SingleAsync(t);
        if (!Eligible(ticket, cutoff)) return false;
        var orders = await clinical.GetServiceOrdersByEncounterIdAsync(ticket.EncounterId, t);
        var item = orders.Where(o => o.EncounterId == ticket.EncounterId).SelectMany(o => o.Items).SingleOrDefault(i => i.Id == ticket.ServiceOrderItemId);
        // A stale queue state must not cancel a sample or service already performed in Clinical.
        if (item is not { ItemStatus: "Ordered" or "Cancelled" }) return false;
        var updated = await db.Set<QueueTicket>().Where(q => q.Id == ticketId && q.IssuedAtUtc < cutoff
            && (q.QueueStatus == QueueStatus.Waiting || q.QueueStatus == QueueStatus.Called))
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.QueueStatus, QueueStatus.Cancelled).SetProperty(q => q.CompletedAtUtc, now), t);
        if (updated == 0) return false;
        await db.Set<ActivePatientEngagement>().Where(e => e.CurrentTicketId == ticketId).ExecuteDeleteAsync(t);
        await db.Set<ActiveStationAllocation>().Where(a => a.CurrentTicketId == ticketId).ExecuteDeleteAsync(t);
        await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), TableName = "scheduling.QueueTicket", RecordId = ticketId,
            ActionType = ActionType.UPDATE, ChangedColumns = "QueueStatus,CompletedAtUtc", ChangeReason = "EndOfServiceDay", TimestampUtc = now,
            OldValuesJson = JsonSerializer.Serialize(new { QueueStatus = ticket.QueueStatus.ToString() }),
            NewValuesJson = JsonSerializer.Serialize(new { QueueStatus = "Cancelled", CompletedAtUtc = now }) }, t);
        await publisher.PublishAsync(new ClsTicketCancelled(Guid.CreateVersion7(), ticketId, ticket.EncounterId, encounter.PatientId,
            ticket.ServiceOrderItemId!.Value, "EndOfServiceDay", now, null), ticketId, t);
        return true;
    }, ct);

    private static bool Eligible(QueueTicket? ticket, DateTime cutoff) => ticket is { TicketType: TicketType.CLS, ServiceOrderItemId: not null }
        && ticket.IssuedAtUtc < cutoff && ticket.QueueStatus is QueueStatus.Waiting or QueueStatus.Called;
}
