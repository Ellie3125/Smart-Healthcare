using System.Text.Json;
using Clinical.Contracts;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Queues;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Scheduling.Infrastructure.Queues;

public sealed class ReturnTicketActivationDeferredException(string code) : Exception(code);

public sealed class ReturnTicketActivationHandler(SchedulingDbContext db, IClinicalModule clinical, IIdentityPatientsModule identity,
    IUnitOfWork uow, IProcessedEventStore processed, IDbClock clock, IIntegrationEventPublisher publisher, AuditLogWriter audit)
    : IIntegrationEventHandler<ResultPublished>
{
    public Task HandleAsync(ResultPublished e, CancellationToken ct)
    {
        if (e.EventId == Guid.Empty || e.EncounterId == Guid.Empty || e.ServiceOrderId == Guid.Empty)
            throw new ArgumentException("Missing event, encounter or service-order identifier.");
        return uow.ExecuteAsync(async t =>
        {
            if (!await processed.TryMarkAsync(e.EventId, "Scheduling.ResultPublished.v1", t)) return;
            if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Scheduling must participate in IUnitOfWork.");
            var encounter = await clinical.GetEncounterByIdAsync(e.EncounterId, t)
                ?? throw new ReturnTicketActivationDeferredException("EncounterNotFound");
            if (encounter.Status is not ("InProgress" or "WaitingCLS" or "ReturningDoctor")) return;
            await db.AppLockAsync(SqlQueueStore.PatientLock(encounter.PatientId), t);
            await db.AppLockAsync($"scheduling:service-order:{e.ServiceOrderId:N}", t);
            var ticket = await db.Set<QueueTicket>().AsNoTracking().SingleOrDefaultAsync(q => q.ServiceOrderId == e.ServiceOrderId
                && q.EncounterId == e.EncounterId && q.TicketType == TicketType.ReturnConsultation, t)
                ?? throw new ReturnTicketActivationDeferredException("ReturnTicketNotFound");
            if (ticket.QueueStatus != QueueStatus.Deferred) return;
            var readiness = ReturnTicketRules.Check(e.EncounterId, e.ServiceOrderId, await clinical.GetServiceOrdersByEncounterIdAsync(e.EncounterId, t));
            if (readiness == ReturnTicketReadiness.Superseded) return;
            if (readiness != ReturnTicketReadiness.Ready) throw new ReturnTicketActivationDeferredException("RequiredResultsNotPublished");
            if (ticket.TargetRoomId != encounter.OriginalRoomId) throw new ReturnTicketActivationDeferredException("OriginalRoomMismatch");
            if (await identity.GetDoctorAsync(encounter.OriginalDoctorId, t) is not { IsActive: true }
                || await identity.GetRoomAsync(encounter.OriginalRoomId, t) is not { IsActive: true })
                throw new ReturnTicketActivationDeferredException("OriginalDoctorOrRoomUnavailable");
            if (await db.Set<ServiceStation>().AnyAsync(x => x.RoomId == ticket.TargetRoomId, t)
                && !await db.Set<ServiceStation>().AnyAsync(x => x.RoomId == ticket.TargetRoomId && x.IsActive, t))
                throw new ReturnTicketActivationDeferredException("OriginalDoctorOrRoomUnavailable");
            if (await db.Set<ActivePatientEngagement>().AnyAsync(x => x.PatientId == encounter.PatientId, t))
                throw new ReturnTicketActivationDeferredException("PatientBusy");
            var now = await clock.GetUtcNowAsync(t);
            var updated = await db.Set<QueueTicket>().Where(q => q.Id == ticket.Id && q.QueueStatus == QueueStatus.Deferred)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.QueueStatus, QueueStatus.Waiting).SetProperty(q => q.ActivatedAtUtc, now), t);
            if (updated == 0) return;
            await audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), TableName = "scheduling.QueueTicket", RecordId = ticket.Id,
                ActionType = ActionType.UPDATE, ChangedColumns = "QueueStatus,ActivatedAtUtc", TimestampUtc = now, OperationId = e.OperationId,
                OldValuesJson = JsonSerializer.Serialize(new { QueueStatus = "Deferred" }),
                NewValuesJson = JsonSerializer.Serialize(new { QueueStatus = "Waiting", ActivatedAtUtc = now }) }, t);
            await publisher.PublishAsync(new ReturnTicketActivated(Guid.CreateVersion7(), e.EncounterId, encounter.PatientId, e.ServiceOrderId,
                ticket.Id, ticket.TargetRoomId, encounter.OriginalDoctorId, ticket.TicketNumber, now, e.OperationId), ticket.Id, t);
        }, ct);
    }
}
