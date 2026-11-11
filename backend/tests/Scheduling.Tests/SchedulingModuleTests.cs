using Microsoft.Extensions.DependencyInjection;
using Scheduling.Application;
using Scheduling.Application.DoctorSearch;
using Scheduling.Contracts;
using Shared.Abstractions;
using Billing.Contracts;
using Xunit;

namespace Scheduling.Tests;

public sealed class SchedulingModuleTests
{
    private static readonly DateOnly ServiceDate = new(2026, 11, 15);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SearchDoctorsReturnsServicePageThroughContract()
    {
        var doctor = new DoctorSearchProfile(Guid.NewGuid(), "BS A", Guid.NewGuid(), Guid.NewGuid(), "Thạc sĩ", "Tim mạch");
        var billing = new DoctorSearchTests.SingleQuoteBilling();
        await using var provider = Provider(doctor, billing);
        var module = provider.GetRequiredService<ISchedulingModule>();

        var page = await module.SearchDoctorsAsync(new DoctorSearchQuery(ServiceDate, doctor.DoctorRankId, doctor.SpecialtyId), Ct);

        var item = Assert.Single(page.Items);
        Assert.Equal(new DoctorSearchItem(doctor.DoctorId, "BS A", doctor.DoctorRankId, doctor.SpecialtyId, 15, 150000m,
            BillingCurrency.Vnd, 30, "Thạc sĩ", "Tim mạch"), item);
        Assert.Equal((ServiceDate, 1, 20, false), (page.ServiceDate, page.Page, page.PageSize, page.HasMore));
        Assert.Equal(doctor.DoctorId, Assert.Single(billing.Quotes).DoctorId);
    }

    public static TheoryData<DoctorSearchQuery?> InvalidQueries => new()
    {
        null!,
        new DoctorSearchQuery(default),
        new DoctorSearchQuery(ServiceDate, DoctorRankId: Guid.Empty),
        new DoctorSearchQuery(ServiceDate, SpecialtyId: Guid.Empty),
        new DoctorSearchQuery(ServiceDate, Page: 0),
        new DoctorSearchQuery(ServiceDate, Page: DoctorSearchQuery.MaxPage + 1),
        new DoctorSearchQuery(ServiceDate, PageSize: 0),
        new DoctorSearchQuery(ServiceDate, PageSize: DoctorSearchQuery.MaxPageSize + 1),
    };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task InvalidQueryThrowsInvalidInputWithoutReadingData(DoctorSearchQuery? query)
    {
        var billing = new DoctorSearchTests.SingleQuoteBilling();
        await using var provider = Provider(new DoctorSearchProfile(Guid.NewGuid(), "BS A", Guid.NewGuid(), Guid.NewGuid()), billing);
        var clock = (DoctorSearchTests.FakeDbClock)provider.GetRequiredService<IDbClock>();

        var error = await Assert.ThrowsAsync<SchedulingException>(
            () => provider.GetRequiredService<ISchedulingModule>().SearchDoctorsAsync(query!, Ct));

        Assert.Equal(SchedulingErrorCode.InvalidInput, error.Code);
        Assert.Equal(0, clock.Calls);
        Assert.Empty(billing.Quotes);
    }

    private static ServiceProvider Provider(DoctorSearchProfile doctor, IBillingModule billing) =>
        new ServiceCollection()
            .AddSchedulingApplication()
            .AddSingleton<IDoctorSearchDirectory>(new FixedDirectory(doctor))
            .AddSingleton<IDoctorSearchQuotaReader>(
                new DoctorSearchTests.Quotas([(ServiceDate, new DoctorSearchQuota(doctor.DoctorId, 30, 2, 10, 3))]))
            .AddSingleton(billing)
            .AddSingleton<IDbClock>(new DoctorSearchTests.FakeDbClock(DoctorSearchTests.CutoffUtc.AddSeconds(-1)))
            .BuildServiceProvider();

    // Fake Identity: danh mục bác sĩ cố định thay adapter IdentityPatients.
    private sealed class FixedDirectory(DoctorSearchProfile doctor) : IDoctorSearchDirectory
    {
        public Task<IReadOnlyList<DoctorSearchProfile>> GetActiveDoctorsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DoctorSearchProfile>>([doctor]);
    }
}
