using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Appointments;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Appointments;

internal sealed class SqlPatientAppointmentReader(SchedulingDbContext db) : IPatientAppointmentReader
{
    public async Task<IReadOnlyList<PatientAppointment>> ListAsync(Guid appUserId, int offset, int limit, CancellationToken ct) =>
        await Project(Rows(appUserId)
                .OrderByDescending(x => x.Quota.ServiceDate).ThenByDescending(x => x.Appointment.ConfirmedAtUtc)
                .ThenBy(x => x.Appointment.Id)
                .Skip(offset).Take(limit))
            .ToListAsync(ct);

    public Task<PatientAppointment?> GetAsync(Guid appointmentId, Guid appUserId, CancellationToken ct) =>
        Project(Rows(appUserId).Where(x => x.Appointment.Id == appointmentId)).SingleOrDefaultAsync(ct);

    // Lọc và sắp xếp trên entity trước khi chiếu sang record để EF dịch được sang SQL.
    private IQueryable<Row> Rows(Guid appUserId) =>
        from appointment in db.Set<Appointment>().AsNoTracking()
        join reservation in db.Set<Reservation>() on appointment.ReservationId equals reservation.Id
        join quota in db.Set<DoctorDayQuota>() on appointment.SessionQuotaId equals quota.Id
        where reservation.AppUserId == appUserId
        select new Row { Appointment = appointment, Quota = quota };

    private static IQueryable<PatientAppointment> Project(IQueryable<Row> rows) =>
        rows.Select(x => new PatientAppointment(x.Appointment.Id, x.Appointment.AppointmentNumber, x.Appointment.Status,
            x.Appointment.PatientId, x.Quota.DoctorId, x.Quota.ServiceDate, x.Quota.AssignedRoomId,
            x.Appointment.AppliedConsultationFee, x.Appointment.ConfirmedAtUtc, x.Quota.CheckInDeadlineUtc,
            x.Appointment.CancelledAtUtc));

    private sealed class Row
    {
        public required Appointment Appointment { get; init; }
        public required DoctorDayQuota Quota { get; init; }
    }
}
