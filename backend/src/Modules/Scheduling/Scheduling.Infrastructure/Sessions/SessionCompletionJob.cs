using System.Text.Json;
using Clinical.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Scheduling.Infrastructure.Sessions;

public sealed class SessionCompletionJob(SchedulingDbContext db, IClinicalModule clinical, IUnitOfWork uow, IDbClock clock, AuditLogWriter audit)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = await clock.GetUtcNowAsync(ct);
        var ids = await db.Set<DoctorDayQuota>().AsNoTracking().Where(q => q.SessionStartTimeUtc <= now
            && (q.SessionStatus == SessionStatus.Scheduled || q.SessionStatus == SessionStatus.InProgress))
            .OrderBy(q => q.ServiceDate).ThenBy(q => q.Id).Select(q => q.Id).ToListAsync(ct);
        var completed = 0;
        foreach (var id in ids) if (await TryCompleteAsync(id, ct)) completed++;
        return completed;
    }

    public Task<bool> TryCompleteAsync(Guid quotaId, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Scheduling must participate in IUnitOfWork.");
        var quota = await db.Set<DoctorDayQuota>().FromSql($"SELECT * FROM scheduling.DoctorDayQuota WITH (UPDLOCK,HOLDLOCK) WHERE Id={quotaId}")
            .AsNoTracking().SingleOrDefaultAsync(t);
        var now = await clock.GetUtcNowAsync(t);
        if (quota is null || quota.SessionStatus is not (SessionStatus.Scheduled or SessionStatus.InProgress) || quota.SessionStartTimeUtc > now
            || quota.HeldCount != 0 || await db.Set<Reservation>().AnyAsync(r => r.SessionQuotaId == quotaId && r.Status == ReservationStatus.Held, t)) return false;
        var appointments = await db.Set<Appointment>().AsNoTracking().Where(a => a.SessionQuotaId == quotaId).ToListAsync(t);
        if (appointments.Count == 0) return false;
        // Initial ticket numbers are scoped by quota (check-in contract); validate the Clinical appointment before using the link.
        var prefix = $"{quotaId:N}-";
        var encounters = await db.Set<QueueTicket>().AsNoTracking().Where(q => q.TicketType == TicketType.InitialConsultation && q.TicketNumber.StartsWith(prefix))
            .Select(q => q.EncounterId).ToListAsync(t);
        foreach (var encounterId in encounters)
        {
            var encounter = await clinical.GetEncounterByIdAsync(encounterId, t);
            if (encounter?.Status != "Completed") continue;
            var appointment = appointments.SingleOrDefault(a => a.Id == encounter.AppointmentId && a.PatientId == encounter.PatientId);
            if (appointment?.Status != AppointmentStatus.CheckedIn) continue;
            if (await db.Set<QueueTicket>().AnyAsync(q => q.EncounterId == encounterId && q.QueueStatus != QueueStatus.Finished && q.QueueStatus != QueueStatus.Cancelled, t)
                || await db.Set<ActivePatientEngagement>().AnyAsync(e => e.EncounterId == encounterId, t)
                || await db.Set<ActiveStationAllocation>().AnyAsync(a => db.Set<QueueTicket>().Any(q => q.Id == a.CurrentTicketId && q.EncounterId == encounterId), t)) continue;
            await db.Set<Appointment>().Where(a => a.Id == appointment.Id && a.Status == AppointmentStatus.CheckedIn)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AppointmentStatus.Completed), t);
            await db.Set<ActiveBookingClaim>().Where(c => c.AppointmentId == appointment.Id && c.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false).SetProperty(c => c.ReleasedAtUtc, now), t);
            await WriteAudit(appointment.Id, "scheduling.Appointment", "Status", new { Status = "CheckedIn" }, new { Status = "Completed" }, now, t);
        }
        if (await db.Set<Appointment>().AnyAsync(a => a.SessionQuotaId == quotaId && a.Status != AppointmentStatus.Completed
            && a.Status != AppointmentStatus.Absent && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.Rescheduled, t)
            || await db.Set<QueueTicket>().AnyAsync(q => encounters.Contains(q.EncounterId) && q.QueueStatus != QueueStatus.Finished && q.QueueStatus != QueueStatus.Cancelled, t)) return false;
        await db.Set<DoctorDayQuota>().Where(q => q.Id == quotaId).ExecuteUpdateAsync(s => s.SetProperty(q => q.SessionStatus, SessionStatus.Completed).SetProperty(q => q.IsOpen, false), t);
        await WriteAudit(quotaId, "scheduling.DoctorDayQuota", "SessionStatus,IsOpen", new { SessionStatus = quota.SessionStatus.ToString(), quota.IsOpen },
            new { SessionStatus = "Completed", IsOpen = false }, now, t);
        return true;
    }, ct);

    private Task WriteAudit(Guid id, string table, string columns, object before, object after, DateTime now, CancellationToken ct) =>
        audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), RecordId = id, TableName = table, ChangedColumns = columns,
            ActionType = ActionType.UPDATE, OldValuesJson = JsonSerializer.Serialize(before), NewValuesJson = JsonSerializer.Serialize(after), TimestampUtc = now }, ct);
}
