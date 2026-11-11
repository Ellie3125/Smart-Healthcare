using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.DoctorDays;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.DoctorDays;

public sealed class SqlDoctorDayStore(SchedulingDbContext db) : IDoctorDayStore
{
    private const string DoctorDateIndex = "IX_DoctorDayQuota_DoctorId_ServiceDate";

    public async Task<DoctorDayQuota?> TryOpenAsync(DoctorDayQuota quota, CancellationToken ct)
    {
        db.Add(quota);
        try
        {
            await db.SaveChangesAsync(ct);
            return quota;
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 } sql
            && sql.Message.Contains(DoctorDateIndex, StringComparison.Ordinal))
        {
            db.Entry(quota).State = EntityState.Detached;
            return await TryReopenAsync(quota.DoctorId, quota.ServiceDate, ct);
        }
    }

    public async Task<DoctorDayResult> CloseAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var quota = await FindForUpdateAsync(doctorId, serviceDate, ct);

        if (quota is null)
        {
            return DoctorDayResult.DayNotFound;
        }

        if (quota.HeldCount != 0 || quota.AdmittedCount != 0 || quota.LockedCancelledCount != 0
            || await db.Set<Reservation>().AnyAsync(x => x.SessionQuotaId == quota.Id
                && (x.Status == ReservationStatus.Held || x.Status == ReservationStatus.Confirmed), ct)
            || await db.Set<Appointment>().AnyAsync(x => x.SessionQuotaId == quota.Id
                && x.Status != AppointmentStatus.Cancelled && x.Status != AppointmentStatus.Rescheduled, ct))
        {
            return DoctorDayResult.HasBookings;
        }

        if (quota.IsOpen)
        {
            await SetOpenAsync(quota.Id, false, ct);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return DoctorDayResult.Closed;
    }

    private async Task<DoctorDayQuota?> TryReopenAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var quota = await FindForUpdateAsync(doctorId, serviceDate, ct);
        if (quota is not { IsOpen: false })
        {
            return null;
        }

        await SetOpenAsync(quota.Id, true, ct);
        var reopened = await db.Set<DoctorDayQuota>().AsNoTracking().SingleAsync(x => x.Id == quota.Id, ct);

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return reopened;
    }

    // Booking writers take the same quota lock before checking IsOpen or changing bookings.
    private Task<DoctorDayQuota?> FindForUpdateAsync(Guid doctorId, DateOnly serviceDate, CancellationToken ct) =>
        db.LockQuotaAsync(doctorId, serviceDate, ct);

    private Task<int> SetOpenAsync(Guid quotaId, bool isOpen, CancellationToken ct) =>
        db.Set<DoctorDayQuota>().Where(x => x.Id == quotaId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsOpen, isOpen), ct);
}
