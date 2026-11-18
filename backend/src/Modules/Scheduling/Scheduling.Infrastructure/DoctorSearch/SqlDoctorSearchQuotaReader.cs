using Microsoft.EntityFrameworkCore;
using Scheduling.Application.DoctorSearch;
using Scheduling.Domain.Entities;
using Scheduling.Domain.Enums;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.DoctorSearch;

internal sealed class SqlDoctorSearchQuotaReader(SchedulingDbContext db) : IDoctorSearchQuotaReader
{
    public async Task<IReadOnlyList<DoctorSearchQuota>> ReadAsync(DateOnly serviceDate, Guid[] doctorIds, DateTime nowUtc,
        int offset, int limit, CancellationToken ct) =>
        await db.Set<DoctorDayQuota>()
            .AsNoTracking()
            .Where(x => x.ServiceDate == serviceDate && doctorIds.Contains(x.DoctorId) && x.IsOpen
                && (x.SessionStatus == SessionStatus.Scheduled || x.SessionStatus == SessionStatus.InProgress)
                && x.BookingCutoffTimeUtc > nowUtc)
            .OrderBy(x => x.DoctorId)
            .Skip(offset)
            .Take(limit)
            .Select(x => new DoctorSearchQuota(x.DoctorId, x.Capacity, x.HeldCount, x.AdmittedCount, x.LockedCancelledCount))
            .ToListAsync(ct);
}
