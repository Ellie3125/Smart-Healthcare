using Clinical.Contracts;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.PatientNotifications;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Infrastructure.Persistence;
using Shared.Abstractions;

namespace Notifications.Infrastructure.PatientNotifications;

public sealed class EfPatientNotificationReader(WorkflowDbContext db, IIdentityPatientsModule identity,
    IClinicalModule clinical, IUnitOfWork uow, IDbClock clock) : IPatientNotificationReader
{
    public async Task<PatientNotificationPage> ListAsync(Guid userId, Guid patientId, bool unreadOnly, int page, int pageSize, CancellationToken ct)
    {
        if (userId == Guid.Empty || patientId == Guid.Empty || page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new PatientNotificationException(PatientNotificationError.InvalidInput);
        if (!await identity.CanAccessPatientAsync(userId, patientId, AccessLevel.Booking, ct))
            throw new PatientNotificationException(PatientNotificationError.NotFound);
        var visibleResults = await VisibleResultEncounters(userId, patientId, ct);
        var query = db.Set<PatientNotification>().AsNoTracking().Where(x => x.PatientId == patientId
            && (x.Kind != PatientNotificationKind.ResultsAvailable || visibleResults.Contains(x.TargetEntityId)));
        var unread = query.Where(x => !db.Set<PatientNotificationRead>().Any(r => r.NotificationId == x.Id && r.AppUserId == userId));
        var unreadCount = await unread.CountAsync(ct);
        var rows = await (unreadOnly ? unread : query).OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(x => new { Notification = x, Read = db.Set<PatientNotificationRead>().Any(r => r.NotificationId == x.Id && r.AppUserId == userId) })
            .ToListAsync(ct);
        return new(rows.Take(pageSize).Select(x => PatientNotificationMessages.Create(x.Notification, x.Read)).ToArray(), unreadCount, rows.Count > pageSize);
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        if (userId == Guid.Empty || notificationId == Guid.Empty) throw new PatientNotificationException(PatientNotificationError.InvalidInput);
        var notification = await db.Set<PatientNotification>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == notificationId, ct);
        if (notification is null || !await identity.CanAccessPatientAsync(userId, notification.PatientId, AccessLevel.Booking, ct)) return false;
        if (notification.Kind == PatientNotificationKind.ResultsAvailable
            && (!await identity.CanAccessPatientAsync(userId, notification.PatientId, AccessLevel.ClinicalRecords, ct)
                || !await clinical.IsPostClsConclusionPublishedAsync(notification.TargetEntityId, ct))) return false;
        return await uow.ExecuteAsync(async t =>
        {
            if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("WorkflowDbContext must participate in IUnitOfWork.");
            await db.Set<PatientNotification>().FromSql($"SELECT * FROM workflow.PatientNotification WITH (UPDLOCK,HOLDLOCK) WHERE Id={notificationId}")
                .AsNoTracking().SingleAsync(t);
            if (!await db.Set<PatientNotificationRead>().AnyAsync(x => x.NotificationId == notificationId && x.AppUserId == userId, t))
            {
                db.Add(new PatientNotificationRead { NotificationId = notificationId, AppUserId = userId, ReadAtUtc = await clock.GetUtcNowAsync(t) });
                await db.SaveChangesAsync(t);
            }
            return true;
        }, ct);
    }

    private async Task<Guid[]> VisibleResultEncounters(Guid userId, Guid patientId, CancellationToken ct)
    {
        if (!await identity.CanAccessPatientAsync(userId, patientId, AccessLevel.ClinicalRecords, ct)) return [];
        var candidates = await db.Set<PatientNotification>().AsNoTracking().Where(x => x.PatientId == patientId && x.Kind == PatientNotificationKind.ResultsAvailable)
            .Select(x => x.TargetEntityId).Distinct().ToListAsync(ct);
        var visible = new List<Guid>();
        // shortcut: Clinical has only a single-encounter visibility check, request a batch contract when notification history grows.
        foreach (var encounter in candidates)
            if (await clinical.IsPostClsConclusionPublishedAsync(encounter, ct)) visible.Add(encounter);
        return visible.ToArray();
    }
}
