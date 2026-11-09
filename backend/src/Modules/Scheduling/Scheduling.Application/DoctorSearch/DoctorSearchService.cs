using Billing.Contracts;

namespace Scheduling.Application.DoctorSearch;

public sealed class DoctorSearchService(IDoctorSearchDirectory directory, IDoctorSearchQuotaReader quotas,
    IBillingModule billing, TimeProvider clock)
{
    public async Task<DoctorSearchPage?> SearchAsync(DoctorSearchQuery query, CancellationToken ct)
    {
        var doctors = (await directory.GetActiveDoctorsAsync(ct))
            .Where(x => (query.DoctorRankId is null || x.DoctorRankId == query.DoctorRankId)
                && (query.SpecialtyId is null || x.SpecialtyId == query.SpecialtyId))
            .ToDictionary(x => x.DoctorId);

        if (doctors.Count == 0)
        {
            return EmptyPage(query);
        }

        var offset = (query.Page - 1) * query.PageSize;
        var rows = await quotas.ReadAsync(query.ServiceDate, doctors.Keys.ToArray(), offset, query.PageSize + 1, ct);
        if (rows.Count == 0)
        {
            return EmptyPage(query);
        }

        if (billing is not IDoctorSearchBilling batchBilling)
        {
            return null;
        }

        var pageRows = rows.Take(query.PageSize).ToArray();
        var atUtc = clock.GetUtcNow().UtcDateTime;
        var feeQueries = pageRows.Select(row => new QuoteExamFee(row.DoctorId, doctors[row.DoctorId].DoctorRankId, atUtc)).ToArray();
        var fees = await batchBilling.QuoteExamFeesAsync(feeQueries, ct);
        var items = new List<DoctorSearchItem>(pageRows.Length);

        foreach (var row in pageRows)
        {
            if (!fees.TryGetValue(row.DoctorId, out var fee))
            {
                return null;
            }

            var doctor = doctors[row.DoctorId];
            var available = (int)Math.Max(0L, (long)row.Capacity - row.HeldCount - row.AdmittedCount - row.LockedCancelledCount);
            items.Add(new DoctorSearchItem(doctor.DoctorId, doctor.FullName, doctor.DoctorRankId, doctor.SpecialtyId,
                available, fee.UnitPrice, fee.Currency));
        }

        return new DoctorSearchPage(query.ServiceDate, query.Page, query.PageSize, rows.Count > query.PageSize, items);
    }

    private static DoctorSearchPage EmptyPage(DoctorSearchQuery query) =>
        new(query.ServiceDate, query.Page, query.PageSize, false, []);
}

public sealed record DoctorSearchProfile(Guid DoctorId, string FullName, Guid DoctorRankId, Guid SpecialtyId);

public sealed record DoctorSearchQuota(Guid DoctorId, int Capacity, int HeldCount, int AdmittedCount, int LockedCancelledCount);

public interface IDoctorSearchDirectory
{
    Task<IReadOnlyList<DoctorSearchProfile>> GetActiveDoctorsAsync(CancellationToken ct);
}

public interface IDoctorSearchQuotaReader
{
    Task<IReadOnlyList<DoctorSearchQuota>> ReadAsync(DateOnly serviceDate, Guid[] doctorIds, int offset, int limit, CancellationToken ct);
}

// Temporary fake Billing capability until Billing.Contracts supports batch exam quotes.
public interface IDoctorSearchBilling : IBillingModule
{
    Task<IReadOnlyDictionary<Guid, FeeQuote>> QuoteExamFeesAsync(IReadOnlyList<QuoteExamFee> queries, CancellationToken ct);
}
