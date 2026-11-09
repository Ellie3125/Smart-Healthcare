using Billing.Contracts;
using Scheduling.Application.DoctorSearch;
using Xunit;

namespace Scheduling.Tests;

public sealed class DoctorSearchTests
{
    private static readonly DateOnly ServiceDate = new(2026, 11, 15);
    private static readonly Guid RankId = Guid.NewGuid();
    private static readonly Guid SpecialtyId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FiltersBeforePagingAndQuotesOnlyTheReturnedDoctorsInOneBatch()
    {
        var first = Doctor("BS A");
        var second = Doctor("BS B");
        var nextPage = Doctor("BS C");
        var wrongRank = Doctor("BS D") with { DoctorRankId = Guid.NewGuid() };
        var wrongSpecialty = Doctor("BS E") with { SpecialtyId = Guid.NewGuid() };
        var otherDay = Doctor("BS F");
        var rows = new[]
        {
            (ServiceDate, new DoctorSearchQuota(first.DoctorId, 30, 2, 10, 3)),
            (ServiceDate, new DoctorSearchQuota(second.DoctorId, 30, 5, 25, 0)),
            (ServiceDate, new DoctorSearchQuota(nextPage.DoctorId, 30, 0, 0, 0)),
            (ServiceDate, new DoctorSearchQuota(wrongRank.DoctorId, 30, 0, 0, 0)),
            (ServiceDate, new DoctorSearchQuota(wrongSpecialty.DoctorId, 30, 0, 0, 0)),
            (ServiceDate.AddDays(1), new DoctorSearchQuota(otherDay.DoctorId, 30, 0, 0, 0))
        };
        var quotas = new Quotas(rows);
        var billing = new BatchBilling();
        var service = new DoctorSearchService(new Directory(first, second, nextPage, wrongRank, wrongSpecialty, otherDay),
            quotas, billing, TimeProvider.System);

        var page = await service.SearchAsync(new(ServiceDate, RankId, SpecialtyId, PageSize: 2), Ct);

        Assert.NotNull(page);
        Assert.True(page.HasMore);
        Assert.Equal(new[] { first.DoctorId, second.DoctorId }, page.Items.Select(x => x.DoctorId));
        Assert.Equal(new[] { 15, 0 }, page.Items.Select(x => x.AvailableSlots));
        Assert.All(page.Items, x => Assert.Equal(BillingCurrency.Vnd, x.Currency));
        Assert.Equal(new[] { 150000m, 200000m }, page.Items.Select(x => x.ExamFee));
        var batch = Assert.Single(billing.Batches);
        Assert.Equal(page.Items.Select(x => x.DoctorId), batch.Select(x => x.DoctorId));
        Assert.All(batch, x =>
        {
            Assert.Equal(RankId, x.DoctorRankId);
            Assert.Equal(DateTimeKind.Utc, x.AtUtc.Kind);
            Assert.Equal(batch[0].AtUtc, x.AtUtc);
        });
        Assert.Equal((ServiceDate, 0, 3), Assert.Single(quotas.Requests));

        var remaining = await service.SearchAsync(new(ServiceDate, RankId, SpecialtyId, Page: 2, PageSize: 2), Ct);

        Assert.NotNull(remaining);
        Assert.False(remaining.HasMore);
        Assert.Equal(nextPage.DoctorId, Assert.Single(remaining.Items).DoctorId);
        Assert.Equal((ServiceDate, 2, 3), quotas.Requests[1]);
    }

    [Fact]
    public async Task OverbookedQuotaIsClampedToZero()
    {
        var doctor = Doctor("BS A");
        var quotas = new Quotas([(ServiceDate, new DoctorSearchQuota(doctor.DoctorId, 30, 10, 30, 0))]);
        var service = new DoctorSearchService(new Directory(doctor), quotas, new BatchBilling(), TimeProvider.System);

        var page = await service.SearchAsync(new(ServiceDate), Ct);

        Assert.NotNull(page);
        Assert.Equal(0, Assert.Single(page.Items).AvailableSlots);
    }

    [Fact]
    public async Task MissingBatchNeverFallsBackToSingleDoctorQuotes()
    {
        var doctor = Doctor("BS A");
        var quotas = new Quotas([(ServiceDate, new DoctorSearchQuota(doctor.DoctorId, 30, 0, 0, 0))]);
        var service = new DoctorSearchService(new Directory(doctor), quotas, new SingleQuoteBilling(), TimeProvider.System);

        Assert.Null(await service.SearchAsync(new(ServiceDate), Ct));
        var empty = await service.SearchAsync(new(ServiceDate.AddDays(1)), Ct);
        Assert.NotNull(empty);
        Assert.Empty(empty.Items);
    }

    private static DoctorSearchProfile Doctor(string name) => new(Guid.NewGuid(), name, RankId, SpecialtyId);

    private sealed class Directory(params DoctorSearchProfile[] doctors) : IDoctorSearchDirectory
    {
        public Task<IReadOnlyList<DoctorSearchProfile>> GetActiveDoctorsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DoctorSearchProfile>>(doctors);
    }

    internal sealed class Quotas((DateOnly Date, DoctorSearchQuota Quota)[] rows) : IDoctorSearchQuotaReader
    {
        public List<(DateOnly Date, int Offset, int Limit)> Requests { get; } = [];

        public Task<IReadOnlyList<DoctorSearchQuota>> ReadAsync(DateOnly serviceDate, Guid[] doctorIds, int offset,
            int limit, CancellationToken ct)
        {
            Requests.Add((serviceDate, offset, limit));
            return Task.FromResult<IReadOnlyList<DoctorSearchQuota>>(rows
                .Where(x => x.Date == serviceDate && doctorIds.Contains(x.Quota.DoctorId))
                .Skip(offset).Take(limit).Select(x => x.Quota).ToList());
        }
    }

    internal class SingleQuoteBilling : IBillingModule
    {
        public Task<FeeQuote> QuoteExamFeeAsync(QuoteExamFee query, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<FeeQuote>> QuoteClsFeesAsync(QuoteClsFees query, CancellationToken ct) => throw new NotSupportedException();
        public Task<ChargeDto> CreateExamChargeAsync(CreateExamCharge command, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChargeDto>> CreateClsChargesAsync(CreateClsCharges command, CancellationToken ct) => throw new NotSupportedException();
        public Task<EncounterCostSummary> GetEncounterCostSummaryAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentDto> CreatePaymentAsync(CreatePayment command, CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class BatchBilling : SingleQuoteBilling, IDoctorSearchBilling
    {
        public List<IReadOnlyList<QuoteExamFee>> Batches { get; } = [];

        public Task<IReadOnlyDictionary<Guid, FeeQuote>> QuoteExamFeesAsync(IReadOnlyList<QuoteExamFee> queries, CancellationToken ct)
        {
            Batches.Add(queries);
            IReadOnlyDictionary<Guid, FeeQuote> quotes = queries.Select((q, index) => (q.DoctorId, Quote: new FeeQuote(
                Guid.NewGuid(), "TEST", ChargeType.ConsultationFee, PriceBasis.Doctor, null, 150000m + 50000m * index, BillingCurrency.Vnd)))
                .ToDictionary(x => x.DoctorId, x => x.Quote);
            return Task.FromResult(quotes);
        }
    }
}
