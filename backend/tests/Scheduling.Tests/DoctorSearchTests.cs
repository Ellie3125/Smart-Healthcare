using Billing.Contracts;
using Scheduling.Application.DoctorSearch;
using Scheduling.Contracts;
using Shared.Abstractions;
using Xunit;

namespace Scheduling.Tests;

public sealed class DoctorSearchTests
{
    private static readonly DateOnly ServiceDate = new(2026, 11, 15);
    internal static readonly DateTime CutoffUtc = ServiceDate.ToDateTime(new TimeOnly(2, 30), DateTimeKind.Utc);
    private static readonly Guid RankId = Guid.NewGuid();
    private static readonly Guid SpecialtyId = Guid.NewGuid();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FiltersBeforePagingAndQuotesOnlyTheReturnedDoctors()
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
        var billing = new SingleQuoteBilling();
        var clock = new FakeDbClock(CutoffUtc.AddSeconds(-1));
        var service = new DoctorSearchService(new Directory(first, second, nextPage, wrongRank, wrongSpecialty, otherDay),
            quotas, billing, clock);

        var page = await service.SearchAsync(new(ServiceDate, RankId, SpecialtyId, PageSize: 2), Ct);

        Assert.NotNull(page);
        Assert.True(page.HasMore);
        Assert.Equal(new[] { first.DoctorId, second.DoctorId }, page.Items.Select(x => x.DoctorId));
        Assert.Equal(new[] { 15, 0 }, page.Items.Select(x => x.AvailableSlots));
        Assert.All(page.Items, x => Assert.Equal(BillingCurrency.Vnd, x.Currency));
        Assert.Equal(new[] { 150000m, 200000m }, page.Items.Select(x => x.ExamFee));
        Assert.Equal(page.Items.Select(x => x.DoctorId), billing.Quotes.Select(x => x.DoctorId));
        Assert.All(billing.Quotes, x =>
        {
            Assert.Equal(RankId, x.DoctorRankId);
            Assert.Equal(DateTimeKind.Utc, x.AtUtc.Kind);
            Assert.Equal(clock.UtcNow, x.AtUtc);
        });
        Assert.Equal(1, clock.Calls);
        Assert.Equal((ServiceDate, clock.UtcNow, 0, 3), Assert.Single(quotas.Requests));

        var remaining = await service.SearchAsync(new(ServiceDate, RankId, SpecialtyId, Page: 2, PageSize: 2), Ct);

        Assert.NotNull(remaining);
        Assert.False(remaining.HasMore);
        Assert.Equal(nextPage.DoctorId, Assert.Single(remaining.Items).DoctorId);
        Assert.Equal((ServiceDate, clock.UtcNow, 2, 3), quotas.Requests[1]);
        Assert.Equal(nextPage.DoctorId, billing.Quotes[2].DoctorId);
        Assert.Equal(3, billing.Quotes.Count);
        Assert.Equal(2, clock.Calls);
    }

    [Fact]
    public async Task OverbookedQuotaIsClampedToZero()
    {
        var doctor = Doctor("BS A");
        var quotas = new Quotas([(ServiceDate, new DoctorSearchQuota(doctor.DoctorId, 30, 10, 30, 0))]);
        var service = new DoctorSearchService(new Directory(doctor), quotas, new SingleQuoteBilling(),
            new FakeDbClock(CutoffUtc.AddSeconds(-1)));

        var page = await service.SearchAsync(new(ServiceDate), Ct);

        Assert.NotNull(page);
        Assert.Equal(0, Assert.Single(page.Items).AvailableSlots);
    }

    [Fact]
    public async Task ShowsSessionOneSecondBeforeCutoffAndHidesItAtCutoff()
    {
        var doctor = Doctor("BS A");
        var quotas = new Quotas([(ServiceDate, new DoctorSearchQuota(doctor.DoctorId, 30, 0, 0, 0))], CutoffUtc);
        var billing = new SingleQuoteBilling();
        var clock = new FakeDbClock(CutoffUtc.AddSeconds(-1));
        var service = new DoctorSearchService(new Directory(doctor), quotas, billing, clock);

        var before = await service.SearchAsync(new(ServiceDate), Ct);
        Assert.Equal(doctor.DoctorId, Assert.Single(before.Items).DoctorId);
        Assert.Equal(clock.UtcNow, Assert.Single(billing.Quotes).AtUtc);

        clock.UtcNow = CutoffUtc;
        var atCutoff = await service.SearchAsync(new(ServiceDate), Ct);
        Assert.Empty(atCutoff.Items);
        Assert.False(atCutoff.HasMore);
        Assert.Single(billing.Quotes);
        Assert.Equal(2, clock.Calls);
    }

    private static DoctorSearchProfile Doctor(string name) => new(Guid.NewGuid(), name, RankId, SpecialtyId);

    private sealed class Directory(params DoctorSearchProfile[] doctors) : IDoctorSearchDirectory
    {
        public Task<IReadOnlyList<DoctorSearchProfile>> GetActiveDoctorsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DoctorSearchProfile>>(doctors);
    }

    internal sealed class FakeDbClock(DateTime utcNow) : IDbClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public int Calls { get; private set; }

        public Task<DateTime> GetUtcNowAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(UtcNow);
        }
    }

    internal sealed class Quotas((DateOnly Date, DoctorSearchQuota Quota)[] rows, DateTime? cutoffUtc = null)
        : IDoctorSearchQuotaReader
    {
        public List<(DateOnly Date, DateTime NowUtc, int Offset, int Limit)> Requests { get; } = [];

        public Task<IReadOnlyList<DoctorSearchQuota>> ReadAsync(DateOnly serviceDate, Guid[] doctorIds, DateTime nowUtc,
            int offset, int limit, CancellationToken ct)
        {
            Requests.Add((serviceDate, nowUtc, offset, limit));
            return Task.FromResult<IReadOnlyList<DoctorSearchQuota>>(rows
                .Where(x => x.Date == serviceDate && doctorIds.Contains(x.Quota.DoctorId)
                    && (cutoffUtc is null || cutoffUtc > nowUtc))
                .Skip(offset).Take(limit).Select(x => x.Quota).ToList());
        }
    }

    internal sealed class SingleQuoteBilling : IBillingModule
    {
        public List<QuoteExamFee> Quotes { get; } = [];

        public Task<FeeQuote> QuoteExamFeeAsync(QuoteExamFee query, CancellationToken ct)
        {
            var price = 150000m + 50000m * Quotes.Count;
            Quotes.Add(query);
            return Task.FromResult(new FeeQuote(Guid.NewGuid(), "TEST", ChargeType.ConsultationFee,
                PriceBasis.Doctor, null, price, BillingCurrency.Vnd));
        }

        public Task<IReadOnlyList<FeeQuote>> QuoteClsFeesAsync(QuoteClsFees query, CancellationToken ct) => throw new NotSupportedException();
        public Task<ChargeDto> CreateExamChargeAsync(CreateExamCharge command, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChargeDto>> CreateClsChargesAsync(CreateClsCharges command, CancellationToken ct) => throw new NotSupportedException();
        public Task<EncounterCostSummary> GetEncounterCostSummaryAsync(Guid encounterId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PaymentDto> CreatePaymentAsync(CreatePayment command, CancellationToken ct) => throw new NotSupportedException();
    }
}
