using IdentityPatients.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Appointments;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Appointments;

internal sealed class SqlPatientAppointmentReader(SchedulingDbContext db, IIdentityPatientsModule identity) : IPatientAppointmentReader
{
    public async Task<IReadOnlyList<PatientAppointment>> ListAsync(Guid appUserId, int offset, int limit, CancellationToken ct)
    {
        // Vài hồ sơ của tài khoản → lọc Appointment theo index PatientId, không quét cả bảng.
        var allowed = await identity.ListAccessiblePatientsAsync(appUserId, AccessLevel.Booking, ct);
        if (allowed.Count == 0) return [];
        return await Project(Rows(db.Set<Appointment>().AsNoTracking().Where(a => allowed.Contains(a.PatientId)))
                .OrderByDescending(x => x.Quota.ServiceDate).ThenByDescending(x => x.Appointment.ConfirmedAtUtc)
                .ThenBy(x => x.Appointment.Id).Skip(offset).Take(limit), appUserId).ToListAsync(ct);
    }

    public async Task<PatientAppointment?> GetAsync(Guid appointmentId, Guid appUserId, CancellationToken ct)
    {
        var row = await Project(Rows(db.Set<Appointment>().AsNoTracking().Where(a => a.Id == appointmentId)), appUserId).SingleOrDefaultAsync(ct);
        return row is not null && (await identity.GetAccessiblePatientsAsync(appUserId, [row.PatientId], AccessLevel.Booking, ct)).Contains(row.PatientId)
            ? row : null;
    }

    // Lọc và sắp xếp trên entity trước khi chiếu sang record để EF dịch được sang SQL.
    private IQueryable<Row> Rows(IQueryable<Appointment> appointments) =>
        from appointment in appointments
        join quota in db.Set<DoctorDayQuota>().AsNoTracking() on appointment.SessionQuotaId equals quota.Id
        join reservation in db.Set<Reservation>().AsNoTracking() on appointment.ReservationId equals reservation.Id
        select new Row { Appointment = appointment, Quota = quota, Reservation = reservation };

    private static IQueryable<PatientAppointment> Project(IQueryable<Row> rows, Guid appUserId) =>
        rows.Select(x => new PatientAppointment(x.Appointment.Id, x.Appointment.AppointmentNumber, x.Appointment.Status,
            x.Appointment.PatientId, x.Quota.DoctorId, x.Quota.ServiceDate, x.Quota.AssignedRoomId,
            x.Appointment.AppliedConsultationFee, x.Appointment.ConfirmedAtUtc, x.Quota.CheckInDeadlineUtc,
            x.Appointment.CancelledAtUtc, x.Quota.RescheduleCutoffTimeUtc, x.Reservation.BookingChannel, x.Reservation.AppUserId == appUserId));

    private sealed class Row
    {
        public required Appointment Appointment { get; init; }
        public required DoctorDayQuota Quota { get; init; }
        public required Reservation Reservation { get; init; }
    }
}
