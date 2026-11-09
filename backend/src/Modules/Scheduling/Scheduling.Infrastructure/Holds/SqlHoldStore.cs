using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Holds;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;

namespace Scheduling.Infrastructure.Holds;

/// <summary>
/// Giữ chỗ theo Giải pháp v2.13 §1.2. Thứ tự khóa: DoctorDayQuota → Reservation → BookingAttempt.
/// Mọi điều kiện được kiểm dưới khóa quota trước khi ghi, nên lệnh bị từ chối không để lại thay đổi nào.
/// </summary>
internal sealed class SqlHoldStore(SchedulingDbContext db, IDbClock dbClock) : IHoldStore
{
    public async Task<HoldOutcome> HoldAsync(HoldRequest request, TimeSpan holdDuration, CancellationToken ct)
    {
        RequireTransaction();
        var quota = await db.LockQuotaAsync(request.DoctorId, request.ServiceDate, ct);
        if (quota is null)
            return new(HoldResult.DayNotFound);

        var now = await dbClock.GetUtcNowAsync(ct); // sau khi nhận khóa quota
        if (!quota.IsOpen || quota.SessionStatus is not (SessionStatus.Scheduled or SessionStatus.InProgress))
            return new(HoldResult.DayClosed);
        if (now >= quota.BookingCutoffTimeUtc)
            return new(HoldResult.CutoffPassed);
        if (quota.HeldCount + quota.AdmittedCount + quota.LockedCancelledCount >= quota.Capacity)
            return new(HoldResult.Full);

        var attempt = (await db.Set<BookingAttempt>()
            .FromSql($"SELECT * FROM [scheduling].[BookingAttempt] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {request.BookingAttemptId}")
            .ToListAsync(ct)).SingleOrDefault();
        if (attempt is null || attempt.PatientId != request.PatientId)
            return new(HoldResult.AttemptNotFound);
        if (attempt.Status is not (BookingAttemptStatus.Draft or BookingAttemptStatus.NeedsReselection))
            return new(HoldResult.InvalidAttemptState);

        // Claim cùng bác sĩ + ngày chỉ được tạo dưới khóa quota này, nên đọc ở đây là đủ; unique có lọc là lớp chặn cuối.
        if (await db.Set<ActiveBookingClaim>().AnyAsync(x => x.DoctorId == request.DoctorId
                && x.ServiceDate == request.ServiceDate && x.PatientId == request.PatientId && x.IsActive, ct))
            return new(HoldResult.AlreadyClaimed);

        var incremented = await db.Set<DoctorDayQuota>()
            .Where(x => x.Id == quota.Id && x.IsOpen
                && x.HeldCount + x.AdmittedCount + x.LockedCancelledCount < x.Capacity)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HeldCount, x => x.HeldCount + 1), ct);
        if (incremented != 1)
            throw new InvalidOperationException("Tăng HeldCount thất bại dù đang giữ khóa quota.");

        var reservation = new Reservation
        {
            Id = Guid.CreateVersion7(),
            SessionQuotaId = quota.Id,
            BookingAttemptId = attempt.Id,
            PatientId = request.PatientId,
            AppUserId = request.AppUserId,
            BookingChannel = request.Channel,
            ReservedAtUtc = now,
            ExpiresAtUtc = now + holdDuration,
            Status = ReservationStatus.Held,
            IdempotencyKey = request.IdempotencyKey,
        };
        db.Add(reservation);
        db.Add(new ActiveBookingClaim
        {
            Id = Guid.CreateVersion7(),
            SessionQuotaId = quota.Id,
            DoctorId = request.DoctorId,
            ServiceDate = request.ServiceDate,
            PatientId = request.PatientId,
            ReservationId = reservation.Id,
            ClaimSource = Enum.Parse<ClaimSource>(request.Channel.ToString()),
            IsActive = true,
            CreatedAtUtc = now,
        });
        attempt.Status = BookingAttemptStatus.Held;
        attempt.CurrentStep = CurrentStep.SlotHeld;
        attempt.SelectedDoctorId = request.DoctorId;
        attempt.SelectedServiceDate = request.ServiceDate;
        attempt.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
        return new(HoldResult.Held, reservation.Id, reservation.ExpiresAtUtc);
    }

    // shortcut: quét Reservation theo Status + ExpiresAtUtc chưa có index riêng; thêm index (migration mới) khi bảng lớn.
    public async Task<IReadOnlyList<Guid>> FindDueAsync(int limit, CancellationToken ct)
    {
        var now = await dbClock.GetUtcNowAsync(ct);
        return await db.Set<Reservation>().AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Held && x.ExpiresAtUtc <= now)
            .OrderBy(x => x.ExpiresAtUtc)
            .Select(x => x.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<bool> ExpireAsync(Guid reservationId, CancellationToken ct)
    {
        RequireTransaction();
        var day = await db.Set<Reservation>().AsNoTracking()
            .Where(x => x.Id == reservationId)
            .Join(db.Set<DoctorDayQuota>(), r => r.SessionQuotaId, q => q.Id, (r, q) => new { q.DoctorId, q.ServiceDate })
            .SingleOrDefaultAsync(ct);
        if (day is null)
            return false;

        var quota = await db.LockQuotaAsync(day.DoctorId, day.ServiceDate, ct);
        var reservation = (await db.Set<Reservation>()
            .FromSql($"SELECT * FROM [scheduling].[Reservation] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {reservationId}")
            .ToListAsync(ct)).Single();
        var now = await dbClock.GetUtcNowAsync(ct); // sau khi nhận khóa quota và reservation
        if (quota is null || reservation.Status != ReservationStatus.Held || reservation.ExpiresAtUtc > now)
            return false;

        reservation.Status = ReservationStatus.Expired;
        var decremented = await db.Set<DoctorDayQuota>()
            .Where(x => x.Id == quota.Id && x.HeldCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HeldCount, x => x.HeldCount - 1), ct);
        if (decremented != 1)
            throw new InvalidOperationException("HeldCount đã bằng 0 khi hết hạn một giữ chỗ Held.");

        await db.Set<ActiveBookingClaim>()
            .Where(x => x.ReservationId == reservationId && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.ReleasedAtUtc, now), ct);
        await db.Set<BookingAttempt>()
            .Where(x => x.Id == reservation.BookingAttemptId
                && (x.Status == BookingAttemptStatus.Held || x.Status == BookingAttemptStatus.WaitingPayment))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingAttemptStatus.NeedsReselection)
                .SetProperty(x => x.CurrentStep, CurrentStep.DoctorSelection)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Giữ chỗ phải chạy bên trong IUnitOfWork.");
    }
}
