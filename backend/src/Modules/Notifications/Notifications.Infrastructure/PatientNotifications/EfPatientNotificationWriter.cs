using Microsoft.EntityFrameworkCore;
using Notifications.Application.PatientNotifications;
using Notifications.Contracts;
using Notifications.Domain.Entities;
using Notifications.Infrastructure.Persistence;
using Shared.Abstractions;
using DomainKind = Notifications.Domain.Enums.PatientNotificationKind;

namespace Notifications.Infrastructure.PatientNotifications;

public sealed class EfPatientNotificationWriter(WorkflowDbContext db, IUnitOfWork uow, IDbClock clock) : IPatientNotificationWriter
{
    public Task<Guid> SaveAsync(CreatePatientNotification c, CancellationToken ct)
    {
        if (c.SourceEventId == Guid.Empty || c.PatientId == Guid.Empty || c.TargetEntityId == Guid.Empty || !Enum.IsDefined(c.Kind))
            throw new PatientNotificationException(PatientNotificationError.InvalidInput);
        return uow.ExecuteAsync(async t =>
        {
            if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("WorkflowDbContext must participate in IUnitOfWork.");
            var kind = c.Kind.ToString();
            var existing = await db.Set<PatientNotification>().FromSql($"""
                SELECT * FROM workflow.PatientNotification WITH (UPDLOCK,HOLDLOCK)
                WHERE SourceEventId={c.SourceEventId} AND PatientId={c.PatientId} AND Kind={kind}
                """).AsNoTracking().SingleOrDefaultAsync(t);
            if (existing is not null)
            {
                if (existing.TargetEntityId != c.TargetEntityId) throw new PatientNotificationException(PatientNotificationError.IdempotencyConflict);
                return existing.Id;
            }
            var notification = new PatientNotification { Id = Guid.CreateVersion7(), SourceEventId = c.SourceEventId,
                PatientId = c.PatientId, Kind = Enum.Parse<DomainKind>(kind), TargetEntityId = c.TargetEntityId,
                CreatedAtUtc = await clock.GetUtcNowAsync(t) };
            db.Add(notification); await db.SaveChangesAsync(t); return notification.Id;
        }, ct);
    }
}
