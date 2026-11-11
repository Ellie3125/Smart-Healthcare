using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Appointments;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;
using Shared.Infrastructure.Persistence.Enums;

namespace Scheduling.Infrastructure.Appointments;

/// <summary>
/// Hủy/đổi lịch. Thứ tự khóa: DoctorDayQuota (nhiều hàng thì theo Id tăng dần) → Appointment. Mọi điều kiện kiểm dưới khóa,
/// sau khi đọc giờ DB, trước khi ghi; bị từ chối thì không có thay đổi nào.
/// </summary>
internal sealed class SqlAppointmentChangeStore(SchedulingDbContext db, IDbClock dbClock, AuditLogWriter audit) : IAppointmentChangeStore
{
    public async Task<ChangeOutcome> CancelAsync(CancelRequest r, Guid operationId, CancellationToken ct)
    {
        RequireTransaction();
        var day = await FindDayAsync(r.AppointmentId, ct);
        if (day is null)
            return new(ChangeResult.AppointmentNotFound);

        var quota = await db.LockQuotaAsync(day.DoctorId, day.ServiceDate, ct)
            ?? throw new InvalidOperationException("Không thấy DoctorDayQuota của lịch hẹn.");
        var appointment = await LockAppointmentAsync(r.AppointmentId, ct);
        var now = await dbClock.GetUtcNowAsync(ct); // sau khi nhận khóa

        if (r.CancelledBy == CancellationInitiator.Patient && appointment.PatientId != r.PatientId)
            return new(ChangeResult.AppointmentNotFound);
        if (r.ExpectedRowVersion is { } version && !version.AsSpan().SequenceEqual(appointment.RowVersion))
            return new(ChangeResult.VersionConflict);
        var allowed = appointment.Status == AppointmentStatus.Scheduled
            || (r.CancelledBy == CancellationInitiator.Receptionist && appointment.Status == AppointmentStatus.CheckedIn);
        if (!allowed)
            return new(ChangeResult.InvalidAppointmentState);
        if (appointment.Status == AppointmentStatus.CheckedIn && string.IsNullOrWhiteSpace(r.Reason))
            return new(ChangeResult.ReasonRequired);

        // Trước 05:00 ngày D trả suất; từ đúng 05:00 khóa suất, tổng chiếm suất không đổi (Giải pháp §10.2).
        var slotReleased = now < quota.RescheduleCutoffTimeUtc;
        var counters = slotReleased
            ? await db.Set<DoctorDayQuota>().Where(x => x.Id == quota.Id && x.AdmittedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AdmittedCount, x => x.AdmittedCount - 1), ct)
            : await db.Set<DoctorDayQuota>().Where(x => x.Id == quota.Id && x.AdmittedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AdmittedCount, x => x.AdmittedCount - 1)
                    .SetProperty(x => x.LockedCancelledCount, x => x.LockedCancelledCount + 1), ct);
        if (counters != 1)
            throw new InvalidOperationException("AdmittedCount đã bằng 0 khi hủy một lịch đang chiếm suất.");

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAtUtc = now;
        appointment.CancellationReason = r.Reason;
        await db.SaveChangesAsync(ct); // EF kiểm RowVersion của bản đã khóa

        var attemptId = await db.Set<Reservation>().Where(x => x.Id == appointment.ReservationId).Select(x => x.BookingAttemptId).SingleAsync(ct);
        await db.Set<Reservation>().Where(x => x.Id == appointment.ReservationId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReservationStatus.Cancelled), ct);
        await db.Set<BookingAttempt>().Where(x => x.Id == attemptId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingAttemptStatus.Cancelled).SetProperty(x => x.UpdatedAtUtc, now), ct);
        // Từ 05:00 giữ claim để không đặt lại cùng bác sĩ/ngày lách hạn tự đổi (Phạm vi §6.2).
        if (slotReleased)
            await ReleaseClaimAsync(appointment.Id, now, ct);

        if (r.CancelledBy == CancellationInitiator.Receptionist)
        {
            await audit.WriteAsync(new AuditLog
            {
                Id = Guid.CreateVersion7(),
                PerformedByUserId = r.AppUserId,
                OperationId = operationId,
                TableName = "scheduling.Appointment",
                RecordId = appointment.Id,
                ActionType = ActionType.CANCEL,
                ChangedColumns = "Status,CancelledAtUtc,CancellationReason",
                OldValuesJson = JsonSerializer.Serialize(new { Status = oldStatus.ToString() }),
                NewValuesJson = JsonSerializer.Serialize(new { Status = nameof(AppointmentStatus.Cancelled), CancelledAtUtc = now, SlotReleased = slotReleased }),
                ChangeReason = r.Reason,
                TimestampUtc = now,
            }, ct);
        }

        return new(ChangeResult.Cancelled, new CancelledInfo(attemptId, appointment.PatientId, day.DoctorId, day.ServiceDate,
            slotReleased, now, quota.RescheduleCutoffTimeUtc));
    }

    public async Task<ChangeOutcome> RescheduleAsync(RescheduleRequest r, decimal targetFee, CancellationToken ct)
    {
        RequireTransaction();
        var day = await FindDayAsync(r.AppointmentId, ct);
        if (day is null)
            return new(ChangeResult.AppointmentNotFound);
        var targetId = await db.Set<DoctorDayQuota>().AsNoTracking()
            .Where(x => x.DoctorId == r.TargetDoctorId && x.ServiceDate == r.TargetServiceDate).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        if (targetId is null)
            return new(ChangeResult.DayNotFound);
        if (targetId == day.QuotaId)
            return new(ChangeResult.SameDay);

        // Hai DoctorDay khóa theo Id tăng dần để hai lệnh đổi chéo nhau không deadlock (Giải pháp §1.2).
        DoctorDayQuota? oldQuota = null, target = null;
        foreach (var (id, doctorId, date) in new[] { (day.QuotaId, day.DoctorId, day.ServiceDate), (targetId.Value, r.TargetDoctorId, r.TargetServiceDate) }
                     .OrderBy(x => x.Item1))
        {
            var locked = await db.LockQuotaAsync(doctorId, date, ct);
            if (id == day.QuotaId) oldQuota = locked; else target = locked;
        }

        var appointment = await LockAppointmentAsync(r.AppointmentId, ct);
        var now = await dbClock.GetUtcNowAsync(ct); // sau khi nhận khóa

        if (appointment.PatientId != r.PatientId)
            return new(ChangeResult.AppointmentNotFound);
        if (r.ExpectedRowVersion is { } version && !version.AsSpan().SequenceEqual(appointment.RowVersion))
            return new(ChangeResult.VersionConflict);
        if (appointment.Status != AppointmentStatus.Scheduled)
            return new(ChangeResult.InvalidAppointmentState);
        if (now >= oldQuota!.RescheduleCutoffTimeUtc)
            return new(ChangeResult.RescheduleCutoffPassed);
        if (targetFee != appointment.AppliedConsultationFee)
            return new(ChangeResult.DifferentFee);
        if (target is null || !target.IsOpen || target.SessionStatus is not (SessionStatus.Scheduled or SessionStatus.InProgress))
            return new(ChangeResult.DayClosed);
        if (now >= target.BookingCutoffTimeUtc)
            return new(ChangeResult.CutoffPassed);
        if (target.HeldCount + target.AdmittedCount + target.LockedCancelledCount >= target.Capacity)
            return new(ChangeResult.Full);
        if (await db.Set<ActiveBookingClaim>().AnyAsync(x => x.DoctorId == r.TargetDoctorId && x.ServiceDate == r.TargetServiceDate
                && x.PatientId == r.PatientId && x.IsActive, ct))
            return new(ChangeResult.AlreadyClaimed);

        var released = await db.Set<DoctorDayQuota>().Where(x => x.Id == oldQuota.Id && x.AdmittedCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AdmittedCount, x => x.AdmittedCount - 1), ct);
        var taken = await db.Set<DoctorDayQuota>().Where(x => x.Id == target.Id
                && x.HeldCount + x.AdmittedCount + x.LockedCancelledCount < x.Capacity)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AdmittedCount, x => x.AdmittedCount + 1), ct);
        if (released != 1 || taken != 1)
            throw new InvalidOperationException("Cập nhật bộ đếm khi đổi lịch thất bại dù đang giữ khóa hai DoctorDay.");

        var oldReservation = await db.Set<Reservation>().AsNoTracking().SingleAsync(x => x.Id == appointment.ReservationId, ct);
        await db.Set<Reservation>().Where(x => x.Id == oldReservation.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReservationStatus.Cancelled), ct);
        await ReleaseClaimAsync(appointment.Id, now, ct);

        var reservation = new Reservation
        {
            Id = Guid.CreateVersion7(),
            SessionQuotaId = target.Id,
            BookingAttemptId = oldReservation.BookingAttemptId,
            PatientId = r.PatientId,
            AppUserId = oldReservation.AppUserId,
            BookingChannel = oldReservation.BookingChannel,
            ReservedAtUtc = now,
            ExpiresAtUtc = now, // đã xác nhận ngay, không có thời gian giữ
            Status = ReservationStatus.Confirmed,
        };
        var newId = Guid.CreateVersion7();
        var replacement = new Appointment
        {
            Id = newId,
            ReservationId = reservation.Id,
            SessionQuotaId = target.Id,
            PatientId = r.PatientId,
            RescheduledFromAppointmentId = appointment.Id,
            AppointmentNumber = AppointmentNumbers.New(r.TargetServiceDate, newId),
            AppliedConsultationFee = appointment.AppliedConsultationFee,
            Status = AppointmentStatus.Scheduled,
            ConfirmedAtUtc = now,
        };
        db.AddRange(reservation, replacement, new ActiveBookingClaim
        {
            Id = Guid.CreateVersion7(),
            SessionQuotaId = target.Id,
            DoctorId = r.TargetDoctorId,
            ServiceDate = r.TargetServiceDate,
            PatientId = r.PatientId,
            ReservationId = reservation.Id,
            AppointmentId = newId,
            ClaimSource = Enum.Parse<ClaimSource>(oldReservation.BookingChannel.ToString()),
            IsActive = true,
            CreatedAtUtc = now,
        });
        appointment.Status = AppointmentStatus.Rescheduled;
        await db.SaveChangesAsync(ct);

        await db.Set<BookingAttempt>().Where(x => x.Id == oldReservation.BookingAttemptId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.SelectedDoctorId, r.TargetDoctorId)
                .SetProperty(x => x.SelectedDoctorRankId, r.TargetDoctorRankId)
                .SetProperty(x => x.SelectedServiceDate, r.TargetServiceDate)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);

        return new(ChangeResult.Rescheduled, Rescheduled: new RescheduledInfo(newId, replacement.AppointmentNumber,
            oldReservation.BookingAttemptId, now));
    }

    private sealed record Day(Guid QuotaId, Guid DoctorId, DateOnly ServiceDate);

    private Task<Day?> FindDayAsync(Guid appointmentId, CancellationToken ct) =>
        db.Set<Appointment>().AsNoTracking()
            .Where(x => x.Id == appointmentId)
            .Join(db.Set<DoctorDayQuota>(), a => a.SessionQuotaId, q => q.Id, (a, q) => new Day(q.Id, q.DoctorId, q.ServiceDate))
            .SingleOrDefaultAsync(ct);

    private async Task<Appointment> LockAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        (await db.Set<Appointment>()
            .FromSql($"SELECT * FROM [scheduling].[Appointment] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {appointmentId}")
            .ToListAsync(ct)).Single();

    private Task<int> ReleaseClaimAsync(Guid appointmentId, DateTime now, CancellationToken ct) =>
        db.Set<ActiveBookingClaim>().Where(x => x.AppointmentId == appointmentId && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.ReleasedAtUtc, now), ct);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Đổi/hủy lịch phải chạy bên trong IUnitOfWork.");
    }
}
