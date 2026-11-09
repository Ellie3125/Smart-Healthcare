using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.DoctorDays;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.DoctorDays;

public sealed class SqlDoctorDayStore(SchedulingDbContext db) : IDoctorDayStore
{
    private const string DoctorDateIndex = "IX_DoctorDayQuota_DoctorId_ServiceDate";

    public async Task<bool> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct)
    {
        db.Add(quota);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 } sql
            && sql.Message.Contains(DoctorDateIndex, StringComparison.Ordinal))
        {
            db.Entry(quota).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // Booking writers take the same quota lock before checking IsOpen or changing bookings.
        var quota = await db.Set<DoctorDayQuota>()
            .FromSql($"""
                SELECT * FROM [scheduling].[DoctorDayQuota] WITH (UPDLOCK, HOLDLOCK)
                WHERE [DoctorId] = {doctorId} AND [ServiceDate] = {serviceDate}
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(ct);

        if (quota is null)
        {
            return DoctorDayResult.DayNotFound;
        }

        if (quota.HeldCount != 0 || quota.AdmittedCount != 0 || quota.LockedCancelledCount != 0
            || await db.Set<Reservation>().AnyAsync(x => x.SessionQuotaId == quota.Id, ct)
            || await db.Set<Appointment>().AnyAsync(x => x.SessionQuotaId == quota.Id, ct))
        {
            return DoctorDayResult.HasBookings;
        }

        if (quota.IsOpen)
        {
            await db.Set<DoctorDayQuota>().Where(x => x.Id == quota.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsOpen, false), ct);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return DoctorDayResult.Closed;
    }
}
