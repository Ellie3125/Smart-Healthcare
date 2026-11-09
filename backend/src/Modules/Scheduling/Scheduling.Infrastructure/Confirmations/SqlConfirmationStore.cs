using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Confirmations;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;

namespace Scheduling.Infrastructure.Confirmations;

/// <summary>Xác nhận lịch từ giữ chỗ. Thứ tự khóa như hết hạn giữ chỗ: DoctorDayQuota → Reservation → BookingAttempt.</summary>
internal sealed class SqlConfirmationStore(SchedulingDbContext db, IDbClock dbClock) : IConfirmationStore
{
    public async Task<ConfirmCandidate?> FindCandidateAsync(Guid bookingAttemptId, CancellationToken ct)
    {
        var row = await db.Set<Reservation>().AsNoTracking()
            .Where(x => x.BookingAttemptId == bookingAttemptId)
            .Join(db.Set<BookingAttempt>(), r => r.BookingAttemptId, a => a.Id, (r, a) => new { r, a.SelectedDoctorRankId })
            .Join(db.Set<DoctorDayQuota>(), x => x.r.SessionQuotaId, q => q.Id, (x, q) => new { x.r, x.SelectedDoctorRankId, q.DoctorId })
            .OrderByDescending(x => x.r.Status == ReservationStatus.Held)
            .ThenByDescending(x => x.r.ReservedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (row is null)
            return null;

        var rank = row.SelectedDoctorRankId
            ?? throw new InvalidOperationException($"BookingAttempt {bookingAttemptId:D} có giữ chỗ nhưng thiếu SelectedDoctorRankId.");
        return new ConfirmCandidate(row.r.Id, row.r.AppUserId, row.DoctorId, rank, row.r.ReservedAtUtc);
    }

    public async Task<ConfirmResult> ConfirmAsync(Guid reservationId, decimal appliedFee, CancellationToken ct)
    {
        RequireTransaction();
        var day = await db.Set<Reservation>().AsNoTracking()
            .Where(x => x.Id == reservationId)
            .Join(db.Set<DoctorDayQuota>(), r => r.SessionQuotaId, q => q.Id, (r, q) => new { q.DoctorId, q.ServiceDate })
            .SingleAsync(ct);
        var quota = await db.LockQuotaAsync(day.DoctorId, day.ServiceDate, ct)
            ?? throw new InvalidOperationException("Không thấy DoctorDayQuota của reservation.");
        var reservation = (await db.Set<Reservation>()
            .FromSql($"SELECT * FROM [scheduling].[Reservation] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {reservationId}")
            .ToListAsync(ct)).Single();
        var now = await dbClock.GetUtcNowAsync(ct); // sau khi nhận khóa quota và reservation

        if (reservation.Status == ReservationStatus.Confirmed)
            return new ConfirmResult.AlreadyConfirmed();
        if (reservation.Status != ReservationStatus.Held)
            return new ConfirmResult.PaidUnallocated($"Reservation{reservation.Status}");
        // Hết hạn thắng dù job chưa chạy: không tạo lịch; job hết hạn sẽ trả suất (Giải pháp §5.1).
        if (reservation.ExpiresAtUtc <= now)
            return new ConfirmResult.PaidUnallocated("ReservationExpiredBeforePayment");

        var moved = await db.Set<DoctorDayQuota>()
            .Where(x => x.Id == quota.Id && x.HeldCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HeldCount, x => x.HeldCount - 1)
                .SetProperty(x => x.AdmittedCount, x => x.AdmittedCount + 1), ct);
        if (moved != 1)
            throw new InvalidOperationException("HeldCount đã bằng 0 khi xác nhận một giữ chỗ Held.");

        var appointmentId = Guid.CreateVersion7();
        var appointment = new Appointment
        {
            Id = appointmentId,
            ReservationId = reservation.Id,
            SessionQuotaId = quota.Id,
            PatientId = reservation.PatientId,
            AppointmentNumber = AppointmentNumbers.New(day.ServiceDate, appointmentId),
            AppliedConsultationFee = appliedFee,
            Status = AppointmentStatus.Scheduled,
            ConfirmedAtUtc = now,
        };
        db.Add(appointment);
        reservation.Status = ReservationStatus.Confirmed; // EF kiểm RowVersion của bản đã khóa
        await db.SaveChangesAsync(ct);

        await db.Set<ActiveBookingClaim>()
            .Where(x => x.ReservationId == reservation.Id && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AppointmentId, appointmentId), ct);
        await db.Set<BookingAttempt>()
            .Where(x => x.Id == reservation.BookingAttemptId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingAttemptStatus.Confirmed)
                .SetProperty(x => x.CurrentStep, CurrentStep.Finalized)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);

        return new ConfirmResult.Confirmed(appointmentId, appointment.AppointmentNumber, reservation.PatientId, day.DoctorId,
            day.ServiceDate, now);
    }

    public async Task MarkPaidUnallocatedAsync(Guid bookingAttemptId, CancellationToken ct)
    {
        RequireTransaction();
        var now = await dbClock.GetUtcNowAsync(ct);
        await db.Set<BookingAttempt>()
            .Where(x => x.Id == bookingAttemptId && x.Status != BookingAttemptStatus.Confirmed)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingAttemptStatus.PaidUnallocated)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Xác nhận lịch phải chạy bên trong IUnitOfWork.");
    }
}
