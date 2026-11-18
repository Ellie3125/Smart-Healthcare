using Microsoft.EntityFrameworkCore;
using Scheduling.Application.Reports;
using Scheduling.Contracts;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.Persistence;

namespace Scheduling.Infrastructure.Reports;

internal sealed class SqlSchedulingReportReader(SchedulingDbContext db) : ISchedulingReportReader
{
    public async Task<IReadOnlyList<SchedulingDailyReport>> ReadAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct)
    {
        var appointments = await db.Set<Appointment>().AsNoTracking()
            .Where(a => a.ConfirmedAtUtc >= fromUtc && a.ConfirmedAtUtc < untilUtc)
            .GroupBy(a => a.ConfirmedAtUtc.AddHours(7).Date)
            .Select(g => new SchedulingDailyReport(DateOnly.FromDateTime(g.Key), g.Count(), 0, 0)).ToListAsync(ct);
        var cancelled = await db.Set<Appointment>().AsNoTracking()
            .Where(a => a.CancelledAtUtc >= fromUtc && a.CancelledAtUtc < untilUtc)
            .GroupBy(a => a.CancelledAtUtc!.Value.AddHours(7).Date)
            .Select(g => new SchedulingDailyReport(DateOnly.FromDateTime(g.Key), 0, g.Count(), 0)).ToListAsync(ct);
        var tickets = await db.Set<QueueTicket>().AsNoTracking()
            .Where(t => t.IssuedAtUtc >= fromUtc && t.IssuedAtUtc < untilUtc)
            .GroupBy(t => t.IssuedAtUtc.AddHours(7).Date)
            .Select(g => new SchedulingDailyReport(DateOnly.FromDateTime(g.Key), 0, 0, g.Count())).ToListAsync(ct);
        return appointments.Concat(cancelled).Concat(tickets).GroupBy(d => d.Date).OrderBy(g => g.Key)
            .Select(g => new SchedulingDailyReport(g.Key, g.Sum(d => d.Appointments), g.Sum(d => d.Cancelled), g.Sum(d => d.Tickets))).ToArray();
    }
}
