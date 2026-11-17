using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.SessionAdministration;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Scheduling.Infrastructure.SessionAdministration;

public sealed class SqlSessionAdministration(SchedulingDbContext db, IIdentityPatientsModule identity, IUnitOfWork uow,
    IDbClock clock, AuditLogWriter audit, IIntegrationEventPublisher events, StaffCallListReader callEvents) : ISessionAdministration
{
    private static readonly JsonSerializerOptions EventJson = new() { Converters = { new JsonStringEnumConverter() } };

    public Task<SessionAdminView> ReplaceDoctorAsync(Guid doctorId, DateOnly date, Guid replacementDoctorId,
        Guid userId, string reason, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        Validate(doctorId, userId, reason);
        if (date == default) throw new SessionAdminException(SessionAdminError.InvalidInput);
        if (replacementDoctorId == Guid.Empty || replacementDoctorId == doctorId) throw new SessionAdminException(SessionAdminError.InvalidInput);
        var replacement = await identity.GetDoctorAsync(replacementDoctorId, t);
        if (replacement is not { IsActive: true }) throw new SessionAdminException(SessionAdminError.DoctorUnavailable);
        DoctorDayQuota? source = null, target = null;
        foreach (var id in new[] { doctorId, replacementDoctorId }.Order())
        {
            var quota = await db.LockQuotaAsync(id, date, t);
            if (id == doctorId) source = quota; else target = quota;
        }
        if (source is null) throw new SessionAdminException(SessionAdminError.DayNotFound);
        if (target is not null) throw new SessionAdminException(SessionAdminError.AlreadyHasSession);
        var now = await BeforeStart(source, t);
        var before = View(source);
        await db.Set<DoctorDayQuota>().Where(x => x.Id == source.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DoctorId, replacementDoctorId), t);
        await db.Set<ActiveBookingClaim>().Where(x => x.SessionQuotaId == source.Id && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DoctorId, replacementDoctorId), t);
        var appointments = await db.Set<Appointment>().AsNoTracking().Where(x => x.SessionQuotaId == source.Id && x.Status == AppointmentStatus.Scheduled).ToListAsync(t);
        foreach (var appointment in appointments)
            await events.PublishAsync(new AppointmentDoctorChanged(Guid.CreateVersion7(), appointment.Id, appointment.PatientId, source.Id,
                doctorId, replacementDoctorId, replacement.FullName, date, reason, now, null), appointment.Id, t);
        var after = before with { DoctorId = replacementDoctorId };
        await Audit("scheduling.DoctorDayQuota", source.Id, userId, reason, before, new { Session = after, ReplacementDoctorName = replacement.FullName }, now, t);
        return after;
    }, ct);

    public Task<SessionAdminView> CancelSessionAsync(Guid doctorId, DateOnly date, Guid userId, string reason, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        Validate(doctorId, userId, reason);
        if (date == default) throw new SessionAdminException(SessionAdminError.InvalidInput);
        var quota = await db.LockQuotaAsync(doctorId, date, t) ?? throw new SessionAdminException(SessionAdminError.DayNotFound);
        if (quota.SessionStatus == SessionStatus.Cancelled) return View(quota);
        var now = await BeforeStart(quota, t);
        var appointments = await db.Set<Appointment>().AsNoTracking().Where(x => x.SessionQuotaId == quota.Id && x.Status == AppointmentStatus.Scheduled).ToListAsync(t);
        var activeIds = appointments.Select(x => x.Id).ToArray();
        var reservationIds = appointments.Select(x => x.ReservationId).ToArray();
        var reservations = await db.Set<Reservation>().AsNoTracking().Where(x => x.SessionQuotaId == quota.Id
            && (x.Status == ReservationStatus.Held || reservationIds.Contains(x.Id))).ToListAsync(t);
        if (quota.AdmittedCount != appointments.Count || quota.HeldCount != reservations.Count(x => x.Status == ReservationStatus.Held))
            throw new SessionAdminException(SessionAdminError.InvalidState);
        var attemptIds = reservations.Select(x => x.BookingAttemptId).ToArray();
        await db.Set<Appointment>().Where(x => activeIds.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AppointmentStatus.Cancelled)
            .SetProperty(x => x.CancelledAtUtc, now).SetProperty(x => x.CancellationReason, reason), t);
        var ids = reservations.Select(x => x.Id).ToArray();
        await db.Set<Reservation>().Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReservationStatus.Cancelled), t);
        await db.Set<BookingAttempt>().Where(x => attemptIds.Contains(x.Id)).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, BookingAttemptStatus.Cancelled).SetProperty(x => x.UpdatedAtUtc, now), t);
        await db.Set<ActiveBookingClaim>().Where(x => x.SessionQuotaId == quota.Id && x.IsActive).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.IsActive, false).SetProperty(x => x.ReleasedAtUtc, now), t);
        await db.Set<DoctorDayQuota>().Where(x => x.Id == quota.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsOpen, false)
            .SetProperty(x => x.SessionStatus, SessionStatus.Cancelled).SetProperty(x => x.HeldCount, 0).SetProperty(x => x.AdmittedCount, 0), t);
        foreach (var appointment in appointments)
        {
            var attempt = reservations.Single(x => x.Id == appointment.ReservationId).BookingAttemptId;
            await events.PublishAsync(new AppointmentCancelled(Guid.CreateVersion7(), appointment.Id, attempt, appointment.PatientId, doctorId,
                date, CancellationInitiator.Hospital, ConsultationRefund.Full, true, now, quota.RescheduleCutoffTimeUtc, now, null), appointment.Id, t);
        }
        var before = View(quota);
        var after = before with { HeldCount = 0, AdmittedCount = 0, IsOpen = false, SessionStatus = "Cancelled" };
        await Audit("scheduling.DoctorDayQuota", quota.Id, userId, reason, before, after, now, t);
        return after;
    }, ct);

    public Task<RoomDisabledView> DisableRoomAsync(Guid roomId, Guid userId, string reason, CancellationToken ct) => uow.ExecuteAsync(async t =>
    {
        Validate(roomId, userId, reason);
        if (await identity.GetRoomAsync(roomId, t) is null) throw new SessionAdminException(SessionAdminError.RoomNotFound);
        var stations = await db.Set<ServiceStation>().FromSql($"SELECT * FROM scheduling.ServiceStation WITH (UPDLOCK,HOLDLOCK) WHERE RoomId={roomId}").AsNoTracking().ToListAsync(t);
        var mappings = await db.Set<MedicalServiceRoom>().FromSql($"SELECT * FROM scheduling.MedicalServiceRoom WITH (UPDLOCK,HOLDLOCK) WHERE RoomId={roomId}").AsNoTracking().ToListAsync(t);
        var stationsChanged = await db.Set<ServiceStation>().Where(x => x.RoomId == roomId && x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), t);
        var mapsChanged = await db.Set<MedicalServiceRoom>().Where(x => x.RoomId == roomId && x.IsActive).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), t);
        var result = new RoomDisabledView(roomId, stationsChanged, mapsChanged);
        var now = await clock.GetUtcNowAsync(t);
        foreach (var station in stations.Where(x => x.IsActive))
            await Audit("scheduling.ServiceStation", station.Id, userId, reason, station, new { station.RoomId, IsActive = false }, now, t);
        foreach (var mapping in mappings.Where(x => x.IsActive))
            await Audit("scheduling.MedicalServiceRoom", mapping.Id, userId, reason, mapping, new { mapping.RoomId, mapping.MedicalServiceId, IsActive = false }, now, t);
        return result;
    }, ct);

    public async Task<StaffCallPage> GetCallListAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize) throw new SessionAdminException(SessionAdminError.InvalidInput);
        var snapshots = await callEvents.ReadAsync([EventType.AppointmentDoctorChanged, EventType.AppointmentCancelled],
            "$.CancelledBy", "Hospital", (page - 1) * pageSize, pageSize + 1, ct);
        var items = new List<StaffCallItem>();
        foreach (var snapshot in snapshots.Take(pageSize))
        {
            if (snapshot.EventType == nameof(AppointmentDoctorChanged))
            {
                var e = JsonSerializer.Deserialize<AppointmentDoctorChanged>(snapshot.PayloadJson, EventJson)!;
                items.Add(new(e.EventId, e.AppointmentId, e.PatientId, e.OldDoctorId, e.NewDoctorId, e.ServiceDate, e.Reason, e.OccurredAtUtc));
            }
            else
            {
                var e = JsonSerializer.Deserialize<AppointmentCancelled>(snapshot.PayloadJson, EventJson)!;
                items.Add(new(e.EventId, e.AppointmentId, e.PatientId, e.DoctorId, null, e.ServiceDate, "Bệnh viện hủy phiên.", e.OccurredAtUtc));
            }
        }
        // shortcut: Identity has no contact lookup contract, enrich this list after a batch contact contract is published.
        return new(items, page, pageSize, snapshots.Count > pageSize, false);
    }

    private async Task<DateTime> BeforeStart(DoctorDayQuota quota, CancellationToken ct)
    {
        var now = await clock.GetUtcNowAsync(ct);
        if (now >= quota.SessionStartTimeUtc) throw new SessionAdminException(SessionAdminError.SessionStarted);
        if (await db.Set<Appointment>().AnyAsync(x => x.SessionQuotaId == quota.Id
            && x.Status != AppointmentStatus.Scheduled && x.Status != AppointmentStatus.Cancelled && x.Status != AppointmentStatus.Rescheduled, ct))
            throw new SessionAdminException(SessionAdminError.AlreadyCheckedIn);
        if (!quota.IsOpen || quota.SessionStatus != SessionStatus.Scheduled) throw new SessionAdminException(SessionAdminError.InvalidState);
        return now;
    }

    private Task Audit(string table, Guid id, Guid user, string reason, object before, object after, DateTime now, CancellationToken ct) =>
        audit.WriteAsync(new AuditLog { Id = Guid.CreateVersion7(), PerformedByUserId = user, TableName = table, RecordId = id,
            ActionType = ActionType.UPDATE, ChangedColumns = table == "scheduling.DoctorDayQuota" ? "DoctorId,IsOpen,SessionStatus,HeldCount,AdmittedCount,ActiveBookingClaim" : "IsActive",
            OldValuesJson = JsonSerializer.Serialize(before), NewValuesJson = JsonSerializer.Serialize(after), ChangeReason = reason, TimestampUtc = now }, ct);
    private static SessionAdminView View(DoctorDayQuota q) => new(q.Id, q.DoctorId, q.AssignedRoomId, q.ServiceDate, q.HeldCount, q.AdmittedCount, q.LockedCancelledCount, q.IsOpen, q.SessionStatus.ToString());
    private static void Validate(Guid id, Guid user, string reason)
    {
        if (id == Guid.Empty || user == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new SessionAdminException(SessionAdminError.InvalidInput);
    }
}
