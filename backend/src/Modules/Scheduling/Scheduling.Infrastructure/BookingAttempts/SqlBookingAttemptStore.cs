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
}
