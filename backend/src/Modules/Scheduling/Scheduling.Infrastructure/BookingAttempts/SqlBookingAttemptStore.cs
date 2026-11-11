using Microsoft.EntityFrameworkCore;
using Scheduling.Application.BookingAttempts;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.BookingAttempts;

internal sealed class SqlBookingAttemptStore(SchedulingDbContext db) : IBookingAttemptStore
{
    public async Task AddAsync(BookingAttempt attempt, CancellationToken ct)
    {
        db.Add(attempt);
        await db.SaveChangesAsync(ct);
    }

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
