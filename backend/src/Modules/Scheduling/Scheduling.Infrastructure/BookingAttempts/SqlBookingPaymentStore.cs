using Microsoft.EntityFrameworkCore;
using Scheduling.Application.BookingAttempts;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;
using Shared.Abstractions;

namespace Scheduling.Infrastructure.BookingAttempts;

internal sealed class SqlBookingPaymentStore(SchedulingDbContext db, IDbClock dbClock) : IBookingPaymentStore
{
    public Task<HeldBooking?> GetLatestHoldAsync(Guid bookingAttemptId, Guid appUserId, CancellationToken ct) =>
        (from attempt in db.Set<BookingAttempt>().AsNoTracking()
         where attempt.Id == bookingAttemptId && attempt.AppUserId == appUserId
            && attempt.SelectedDoctorId != null && attempt.SelectedDoctorRankId != null
         from reservation in db.Set<Reservation>()
             .Where(x => x.BookingAttemptId == attempt.Id && x.AppUserId == appUserId)
             .OrderByDescending(x => x.ReservedAtUtc).ThenByDescending(x => x.Id)
             .Take(1)
         select new HeldBooking(attempt.PatientId, attempt.Status, attempt.SelectedDoctorId!.Value,
             attempt.SelectedDoctorRankId!.Value, reservation.Id, reservation.Status, reservation.ReservedAtUtc,
             reservation.ExpiresAtUtc))
        .SingleOrDefaultAsync(ct);

    public async Task MarkWaitingPaymentAsync(Guid bookingAttemptId, CancellationToken ct)
    {
        var now = await dbClock.GetUtcNowAsync(ct);
        await db.Set<BookingAttempt>()
            .Where(x => x.Id == bookingAttemptId && x.Status == BookingAttemptStatus.Held)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingAttemptStatus.WaitingPayment)
                .SetProperty(x => x.CurrentStep, CurrentStep.PaymentInitiated)
                .SetProperty(x => x.UpdatedAtUtc, now), ct);
    }
}
