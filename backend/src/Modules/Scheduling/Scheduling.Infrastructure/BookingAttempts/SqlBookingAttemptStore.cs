using Microsoft.EntityFrameworkCore;
using Scheduling.Application.BookingAttempts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.BookingAttempts;

internal sealed class SqlBookingAttemptStore(SchedulingDbContext db) : IBookingAttemptStore
{
    public async Task AddAsync(BookingAttempt attempt, CancellationToken ct)
    {
        db.Add(attempt);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RecoverableAttempt>> ListUnfinishedAsync(Guid appUserId, DateOnly fromDate, int take,
        CancellationToken ct) =>
        await db.Set<BookingAttempt>().AsNoTracking()
            .Where(a => a.AppUserId == appUserId && a.SelectedServiceDate >= fromDate
                && (a.Status == BookingAttemptStatus.Draft || a.Status == BookingAttemptStatus.Held
                    || a.Status == BookingAttemptStatus.WaitingPayment || a.Status == BookingAttemptStatus.Confirming
                    || a.Status == BookingAttemptStatus.NeedsReselection))
            .OrderByDescending(a => a.UpdatedAtUtc).ThenByDescending(a => a.Id).Take(take)
            .Select(a => new RecoverableAttempt(a.Id, a.PatientId, a.SelectedServiceDate, a.SelectedDoctorId))
            .ToListAsync(ct);

    public Task<BookingProgressSnapshot?> GetOwnedAsync(Guid bookingAttemptId, Guid appUserId, CancellationToken ct) =>
        db.Set<BookingAttempt>()
            .AsNoTracking()
            .Where(attempt => attempt.Id == bookingAttemptId && attempt.AppUserId == appUserId)
            .Select(attempt => new BookingProgressSnapshot(attempt.PatientId, attempt.Status, attempt.CurrentStep,
                db.Set<Reservation>()
                    .Where(reservation => reservation.BookingAttemptId == attempt.Id && reservation.AppUserId == appUserId)
                    .OrderByDescending(reservation => reservation.ReservedAtUtc)
                    .ThenByDescending(reservation => reservation.Id)
                    .Select(reservation => new ReservationProgress(reservation.Status, reservation.ExpiresAtUtc))
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(ct);
}
