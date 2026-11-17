using Billing.Contracts;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.WalkIn;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.WalkIn;

internal sealed class SqlWalkInStore(SchedulingDbContext db, OutboxReader outbox) : IWalkInStore
{
    public Task<PaymentVerified> ReadPaymentAsync(Guid paymentIntentId, CancellationToken ct) =>
        outbox.ReadSingleAsync<PaymentVerified>(paymentIntentId, ct);

    public async Task<(Guid Id, string Number)> GetAppointmentAsync(Guid reservationId, CancellationToken ct)
    {
        var row = await db.Set<Appointment>().AsNoTracking().Where(x => x.ReservationId == reservationId)
            .Select(x => new { x.Id, x.AppointmentNumber }).SingleAsync(ct);
        return (row.Id, row.AppointmentNumber);
    }
}
