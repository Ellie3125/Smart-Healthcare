using IdentityPatients.Contracts;
using Billing.Contracts;
using Scheduling.Contracts;
using Shared.Abstractions;

namespace Scheduling.Application.DoctorSearch;

public sealed class DoctorSearchService(IDoctorSearchDirectory directory, IDoctorSearchQuotaReader quotas,
    IBillingModule billing, IDbClock dbClock)
{
    public async Task<DoctorSearchPage> SearchAsync(DoctorSearchQuery query, CancellationToken ct)
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
        var nowUtc = await dbClock.GetUtcNowAsync(ct);
        var rows = await quotas.ReadAsync(query.ServiceDate, doctors.Keys.ToArray(), nowUtc, offset, query.PageSize + 1, ct);
        var items = new List<DoctorSearchItem>(Math.Min(rows.Count, query.PageSize));

        // shortcut: N lời gọi báo giá mỗi trang (≤ 50), đổi sang batch khi Billing có QuoteExamFeesAsync
        foreach (var row in rows.Take(query.PageSize))
        {
            var doctor = doctors[row.DoctorId];
            var fee = await billing.QuoteExamFeeAsync(new QuoteExamFee(doctor.DoctorId, doctor.DoctorRankId, nowUtc), ct);
            var available = (int)Math.Max(0L, (long)row.Capacity - row.HeldCount - row.AdmittedCount - row.LockedCancelledCount);
            items.Add(new DoctorSearchItem(doctor.DoctorId, doctor.FullName, doctor.DoctorRankId, doctor.SpecialtyId,
                available, fee.UnitPrice, fee.Currency, row.Capacity, doctor.DoctorRankName, doctor.SpecialtyName));
        }

        return new DoctorSearchPage(query.ServiceDate, query.Page, query.PageSize, rows.Count > query.PageSize, items);
    }

    private static DoctorSearchPage EmptyPage(DoctorSearchQuery query) =>
        new(query.ServiceDate, query.Page, query.PageSize, false, []);
}

public sealed record DoctorSearchProfile(Guid DoctorId, string FullName, Guid DoctorRankId, Guid SpecialtyId,
    string? DoctorRankName = null, string? SpecialtyName = null);

public sealed record DoctorSearchQuota(Guid DoctorId, int Capacity, int HeldCount, int AdmittedCount, int LockedCancelledCount);

public interface IDoctorSearchDirectory
{
    Task<IReadOnlyList<DoctorSearchProfile>> GetActiveDoctorsAsync(CancellationToken ct);
}

/// <summary>Bác sĩ đang hoạt động kèm tên chuyên khoa, cấp bậc từ module Identity.</summary>
public sealed class IdentityDoctorSearchDirectory(IIdentityPatientsModule identity) : IDoctorSearchDirectory
{
    public async Task<IReadOnlyList<DoctorSearchProfile>> GetActiveDoctorsAsync(CancellationToken ct)
    {
        var specialties = (await identity.GetSpecialtiesAsync(ct)).ToDictionary(x => x.Id, x => x.Name);
        return (await identity.SearchDoctorsAsync(new IdentityPatients.Contracts.DoctorSearch(IsActive: true), ct))
            .Select(x => new DoctorSearchProfile(x.DoctorId, x.FullName, x.RankId, x.SpecialtyId, x.RankName,
                specialties.GetValueOrDefault(x.SpecialtyId)))
            .ToList();
    }
}

public interface IDoctorSearchQuotaReader
{
    Task<IReadOnlyList<DoctorSearchQuota>> ReadAsync(DateOnly serviceDate, Guid[] doctorIds, DateTime nowUtc,
        int offset, int limit, CancellationToken ct);
}
