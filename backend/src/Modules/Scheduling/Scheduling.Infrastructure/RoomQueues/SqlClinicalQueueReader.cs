using Microsoft.EntityFrameworkCore;
using Scheduling.Application.RoomQueues;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.RoomQueues;

internal sealed class SqlClinicalQueueReader(SchedulingDbContext db) : IClinicalQueueReader
{
    public async Task<ClinicalQueuePage> ReadAsync(Guid roomId, DateTime atUtc, int page, int pageSize, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(atUtc.AddHours(7));
        var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var rows = await db.Set<QueueTicket>().AsNoTracking()
            .Where(t => t.TargetRoomId == roomId && t.TicketType == TicketType.InitialConsultation
                && t.QueuedAtUtc >= start && t.QueuedAtUtc < start.AddDays(1)
                && (t.QueueStatus == QueueStatus.Waiting || t.QueueStatus == QueueStatus.Called
                    || t.QueueStatus == QueueStatus.InProgress || t.QueueStatus == QueueStatus.Deferred))
            .OrderBy(t => t.QueuedAtUtc).ThenBy(t => t.SequenceNumber).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(t => new ClinicalQueueTicket(t.Id, t.EncounterId, t.TicketNumber, t.QueueStatus.ToString(), t.QueuedAtUtc))
            .ToListAsync(ct);
        return new(rows.Count > pageSize, rows.Take(pageSize).ToArray());
    }

    public async Task<PatientJourneySnapshot> ReadPatientJourneyAsync(Guid patientId, Guid? appointmentId, Guid? encounterId, DateTime atUtc, CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(atUtc.AddHours(7));
        var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var appointments = from a in db.Set<Appointment>().AsNoTracking() join q in db.Set<DoctorDayQuota>() on a.SessionQuotaId equals q.Id
            where a.PatientId == patientId && q.ServiceDate == date
                && (appointmentId.HasValue ? a.Id == appointmentId : a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.Completed)
            select new { Appointment = a, q.AssignedRoomId };
        var appointment = await appointments.OrderBy(x => x.Appointment.Status == AppointmentStatus.CheckedIn ? 0 : x.Appointment.Status == AppointmentStatus.Scheduled ? 1 : 2)
            .ThenByDescending(x => x.Appointment.ConfirmedAtUtc).ThenBy(x => x.Appointment.Id).FirstOrDefaultAsync(ct);
        var tickets = encounterId is null ? [] : await db.Set<QueueTicket>().AsNoTracking()
            .Where(t => t.EncounterId == encounterId && t.QueuedAtUtc >= start && t.QueuedAtUtc < start.AddDays(1) && t.QueueStatus != QueueStatus.Cancelled)
            .OrderBy(t => t.QueuedAtUtc).ThenBy(t => t.SequenceNumber).ThenBy(t => t.Id)
            .Select(t => new JourneyQueueTicket(t.Id, t.TargetRoomId, t.ServiceOrderItemId, t.TicketNumber, t.TicketType.ToString(), t.QueueStatus.ToString(),
                t.QueuedAtUtc, t.CalledAtUtc, t.ServedAtUtc, t.CompletedAtUtc)).ToListAsync(ct);
        return new(appointment?.Appointment.Id, appointment?.AssignedRoomId, appointment?.Appointment.Status.ToString(), tickets);
    }
}
