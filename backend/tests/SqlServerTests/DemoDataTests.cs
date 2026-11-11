using Billing.Domain.Entities;
using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scheduling.Application.DoctorDays;
using Scheduling.Domain.Entities;
using Scheduling.Infrastructure.DoctorDays;
using Scheduling.Infrastructure.Persistence;
using SmartHealthcare.Api;
using Xunit;

namespace SqlServerTests;

public sealed class DemoDataTests
{
    private static readonly DateOnly Today = new(2031, 3, 10);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seeds_price_list_and_doctor_days_once_and_keeps_closed_days_closed()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using var billing = database.CreateContext<BillingDbContext>(options => new(options));
        await using var scheduling = database.CreateContext<SchedulingDbContext>(options => new(options));
        var store = new SqlDoctorDayStore(scheduling);
        var days = new DoctorDayService(new Rooms(), store, new(new TimeOnly(11, 0)));
        var doctorIds = DemoData.DoctorRooms.Select(x => x.DoctorId).ToArray();

        await DemoData.SeedAsync(billing, scheduling, days, Today, Ct);
        Assert.Equal(DoctorDayResult.Closed, await store.CloseAsync(doctorIds[0], Today, Ct));
        await DemoData.SeedAsync(billing, scheduling, days, Today, Ct);

        var priceList = await billing.Set<PriceList>().AsNoTracking().SingleAsync(x => x.PriceListCode == DemoData.PriceListCode, Ct);
        Assert.True(priceList.IsEffectiveAt(DateTime.UtcNow));
        Assert.Equal(6, await billing.Set<PriceListItem>().CountAsync(x => x.PriceListId == priceList.Id, Ct));

        var quotas = await scheduling.Set<DoctorDayQuota>().AsNoTracking()
            .Where(x => doctorIds.Contains(x.DoctorId) && x.ServiceDate >= Today).ToListAsync(Ct);
        Assert.Equal(doctorIds.Length * DemoData.DaysAhead, quotas.Count);
        Assert.Equal(Today.AddDays(DemoData.DaysAhead - 1), quotas.Max(x => x.ServiceDate));
        var closed = Assert.Single(quotas, x => !x.IsOpen);
        Assert.Equal((doctorIds[0], Today), (closed.DoctorId, closed.ServiceDate));
    }

    private sealed class Rooms : IDoctorDirectory
    {
        public Task<Guid?> GetAssignedRoomIdAsync(Guid doctorId, CancellationToken ct) =>
            Task.FromResult<Guid?>(DemoData.DoctorRooms.Single(x => x.DoctorId == doctorId).RoomId);
    }
}
