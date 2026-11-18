using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Appointments;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Appointments;

internal sealed class SqlPatientAppointmentReader(SchedulingDbContext db) : IPatientAppointmentReader
{
    public async Task<IReadOnlyList<PatientAppointment>> ListAsync(Guid appUserId, int offset, int limit, CancellationToken ct)
    {
        // Hai bước: một câu join + OFFSET/FETCH khiến SQL Server đặt row goal, quét mọi Appointment theo thứ tự sắp xếp rồi dò
        // Reservation (đo 17/11: 29.852 reads). Lấy Reservation của người gọi trước rồi lọc theo danh sách: ~550 reads.
        // shortcut: nạp mọi Reservation của một tài khoản; nếu một tài khoản có hàng nghìn lượt đặt thì thêm cột người đặt vào Appointment.
        var reservationIds = await db.Set<Reservation>().AsNoTracking().Where(r => r.AppUserId == appUserId)
            .Select(r => r.Id).ToListAsync(ct);
        if (reservationIds.Count == 0)
            return [];
        return await Project(Rows(db.Set<Appointment>().AsNoTracking().Where(a => reservationIds.Contains(a.ReservationId)))
                .OrderByDescending(x => x.Quota.ServiceDate).ThenByDescending(x => x.Appointment.ConfirmedAtUtc)
                .ThenBy(x => x.Appointment.Id)
                .Skip(offset).Take(limit))
            .ToListAsync(ct);
    }

    public Task<PatientAppointment?> GetAsync(Guid appointmentId, Guid appUserId, CancellationToken ct) =>
        Project(Rows(db.Set<Appointment>().AsNoTracking().Where(a => a.Id == appointmentId
            && db.Set<Reservation>().Any(r => r.Id == a.ReservationId && r.AppUserId == appUserId)))).SingleOrDefaultAsync(ct);

    // Lọc và sắp xếp trên entity trước khi chiếu sang record để EF dịch được sang SQL.
    private IQueryable<Row> Rows(IQueryable<Appointment> appointments) =>
        from appointment in appointments
        join quota in db.Set<DoctorDayQuota>().AsNoTracking() on appointment.SessionQuotaId equals quota.Id
        select new Row { Appointment = appointment, Quota = quota };

    private static IQueryable<PatientAppointment> Project(IQueryable<Row> rows) =>
        rows.Select(x => new PatientAppointment(x.Appointment.Id, x.Appointment.AppointmentNumber, x.Appointment.Status,
            x.Appointment.PatientId, x.Quota.DoctorId, x.Quota.ServiceDate, x.Quota.AssignedRoomId,
            x.Appointment.AppliedConsultationFee, x.Appointment.ConfirmedAtUtc, x.Quota.CheckInDeadlineUtc,
            x.Appointment.CancelledAtUtc, x.Quota.RescheduleCutoffTimeUtc));

    private sealed class Row
    {
        public required Appointment Appointment { get; init; }
        public required DoctorDayQuota Quota { get; init; }
    }
}
