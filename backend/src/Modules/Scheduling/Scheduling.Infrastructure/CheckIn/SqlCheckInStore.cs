using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.CheckIn;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Scheduling.Infrastructure.CheckIn;

internal sealed class SqlCheckInStore(SchedulingDbContext db, IDbClock clock, AuditLogWriter audit) : ICheckInStore
{
    public async Task<CheckInOutcome> CheckInAsync(CheckInAppointment command,
        Func<AppointmentForCheckIn, CancellationToken, Task<Guid>> getEncounter, CancellationToken ct)
    {
        RequireTransaction();
        var quotaId = await db.Set<Appointment>().Where(x => x.Id == command.AppointmentId)
            .Select(x => (Guid?)x.SessionQuotaId).SingleOrDefaultAsync(ct);
        if (quotaId is null)
            return new(Error: SchedulingErrorCode.AppointmentNotFound);
        var quota = await LockQuotaAsync(quotaId.Value, ct);
        var appointment = await db.Set<Appointment>().FromSql($"""
            SELECT * FROM scheduling.Appointment WITH (UPDLOCK, HOLDLOCK) WHERE Id = {command.AppointmentId}
            """).AsNoTracking().SingleAsync(ct);
        var now = await clock.GetUtcNowAsync(ct);

        if (appointment.Status == AppointmentStatus.CheckedIn)
        {
            var encounterId = await getEncounter(new(appointment.Id, appointment.PatientId, quota.DoctorId, quota.AssignedRoomId, true), ct);
            var ticket = await db.Set<QueueTicket>().AsNoTracking().SingleAsync(x => x.EncounterId == encounterId
                && x.TicketType == TicketType.InitialConsultation, ct);
            return new(ToResult(appointment, quota, ticket, true), ticket.TargetRoomId);
        }
        if (appointment.Status != AppointmentStatus.Scheduled)
            return new(Error: SchedulingErrorCode.InvalidAppointmentState);
        if (now >= Deadline(quota))
        {
            await MarkScheduledAbsentAsync(quota.Id, ct);
            return new(Error: SchedulingErrorCode.CheckInDeadlinePassed);
        }
        if (DateOnly.FromDateTime(now.AddHours(7)) != quota.ServiceDate)
            return new(Error: SchedulingErrorCode.CheckInNotOpen);
        if (!quota.IsOpen || quota.SessionStatus is not (SessionStatus.Scheduled or SessionStatus.InProgress))
            return new(Error: SchedulingErrorCode.InvalidAppointmentState);

        var openedId = await getEncounter(new(appointment.Id, appointment.PatientId, quota.DoctorId, quota.AssignedRoomId, false), ct);

        // Prefix quota giữ phạm vi số theo bác sĩ/phiên, không phụ thuộc phòng hoặc trạng thái vé.
        var prefix = $"{quota.Id:N}-";
        var sequence = await db.NextAsync(prefix, 1, ct);
        var created = new QueueTicket
        {
            Id = Guid.CreateVersion7(), EncounterId = openedId, TargetRoomId = quota.AssignedRoomId,
            TicketType = TicketType.InitialConsultation, QueueStatus = QueueStatus.Waiting,
            SequenceNumber = sequence, TicketNumber = $"{prefix}{sequence:D3}", IssuedAtUtc = now,
        };
        db.Add(created);
        await db.Set<Appointment>().Where(x => x.Id == appointment.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AppointmentStatus.CheckedIn), ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditLog
        {
            Id = Guid.CreateVersion7(), PerformedByUserId = command.PerformedByUserId, OperationId = command.OperationId,
            TableName = "scheduling.Appointment", RecordId = appointment.Id, ActionType = ActionType.UPDATE,
            ChangedColumns = "Status", OldValuesJson = "{\"Status\":\"Scheduled\"}",
            NewValuesJson = JsonSerializer.Serialize(new { Status = "CheckedIn", created.EncounterId, QueueTicketId = created.Id }),
            TimestampUtc = now,
        }, ct);
        return new(ToResult(appointment, quota, created, false), created.TargetRoomId);
    }

    public async Task<IReadOnlyList<TodayAppointment>> ListTodayAsync(Guid? doctorId, string? appointmentNumber,
        int skip, int take, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime((await clock.GetUtcNowAsync(ct)).AddHours(7));
        return await db.Set<Appointment>().AsNoTracking()
            .Join(db.Set<DoctorDayQuota>(), a => a.SessionQuotaId, q => q.Id, (a, q) => new { a, q })
            .Where(x => x.q.ServiceDate == today && (doctorId == null || x.q.DoctorId == doctorId)
                && (appointmentNumber == null || x.a.AppointmentNumber == appointmentNumber))
            .OrderBy(x => x.a.ConfirmedAtUtc).ThenBy(x => x.a.Id).Skip(skip).Take(take)
            .Select(x => new TodayAppointment(x.a.Id, x.a.AppointmentNumber, x.a.PatientId, x.q.DoctorId,
                x.q.AssignedRoomId, x.q.ServiceDate, x.a.Status.ToString(),
                x.q.CheckInDeadlineUtc ?? x.q.ServiceDate.ToDateTime(new TimeOnly(4, 0))))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> FindOverdueQuotasAsync(int take, CancellationToken ct)
    {
        var now = await clock.GetUtcNowAsync(ct);
        var today = DateOnly.FromDateTime(now.AddHours(7));
        var pastDefault = now >= DateTime.SpecifyKind(today.ToDateTime(new TimeOnly(4, 0)), DateTimeKind.Utc);
        return await db.Set<DoctorDayQuota>().Where(q =>
                (q.CheckInDeadlineUtc != null ? q.CheckInDeadlineUtc <= now : q.ServiceDate < today || (q.ServiceDate == today && pastDefault))
                && db.Set<Appointment>().Any(a => a.SessionQuotaId == q.Id && a.Status == AppointmentStatus.Scheduled))
            .OrderBy(q => q.ServiceDate).ThenBy(q => q.Id).Take(take).Select(q => q.Id).ToListAsync(ct);
    }

    public async Task<int> MarkAbsentAsync(Guid quotaId, CancellationToken ct)
    {
        RequireTransaction();
        var quota = await LockQuotaAsync(quotaId, ct);
        return await clock.GetUtcNowAsync(ct) >= Deadline(quota) ? await MarkScheduledAbsentAsync(quota.Id, ct) : 0;
    }

    private Task<int> MarkScheduledAbsentAsync(Guid quotaId, CancellationToken ct) => db.Set<Appointment>()
        .Where(x => x.SessionQuotaId == quotaId && x.Status == AppointmentStatus.Scheduled)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, AppointmentStatus.Absent), ct);

    private async Task<DoctorDayQuota> LockQuotaAsync(Guid id, CancellationToken ct)
    {
        return await db.LockQuotaByIdAsync(id, ct) ?? throw new InvalidOperationException("Không thấy quota của lịch hẹn.");
    }

    private static DateTime Deadline(DoctorDayQuota quota) => quota.CheckInDeadlineUtc
        ?? DateTime.SpecifyKind(quota.ServiceDate.ToDateTime(new TimeOnly(4, 0)), DateTimeKind.Utc);

    private static CheckInResult ToResult(Appointment a, DoctorDayQuota q, QueueTicket t, bool replay) =>
        new(a.Id, a.PatientId, q.DoctorId, q.ServiceDate, t.IssuedAtUtc, replay, t.Id, t.TicketNumber, t.SequenceNumber, t.EncounterId);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Check-in và CheckInDeadline phải chạy bên trong IUnitOfWork.");
    }
}
