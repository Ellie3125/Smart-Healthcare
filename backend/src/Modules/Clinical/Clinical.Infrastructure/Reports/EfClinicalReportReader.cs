using Clinical.Application.Reports;
using Clinical.Contracts;
using Clinical.Domain.Entities;
using Clinical.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinical.Infrastructure.Reports;

internal sealed class EfClinicalReportReader(ClinicalDbContext db) : IClinicalReportReader
{
    public async Task<IReadOnlyList<ClinicalDailyReport>> ReadAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct) =>
        await db.Set<Encounter>().AsNoTracking().Where(e => e.CheckedInAtUtc >= fromUtc && e.CheckedInAtUtc < untilUtc)
            .GroupBy(e => e.CheckedInAtUtc.AddHours(7).Date)
            .Select(g => new ClinicalDailyReport(DateOnly.FromDateTime(g.Key), g.Count())).ToListAsync(ct);
}
