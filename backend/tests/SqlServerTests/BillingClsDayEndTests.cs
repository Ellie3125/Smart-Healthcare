using Billing.Application;
using Billing.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Job cuối ngày khám D trên SQL Server thật. Dữ liệu giả lập, rollback sau mỗi test.
// Job quét toàn bảng billing.BillingItem nên chỉ kiểm các khoản do test tạo.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingClsDayEndTests
{
    [Fact]
    public async Task Unpaid_cls_charges_of_past_days_are_cancelled_others_are_kept()
    {
        await using var db = await OpenAsync();
        var price = await PriceItemAsync(db);
        var past = ClsBatches.StartOfDayUtc(DateTime.UtcNow).AddMinutes(-1); // 23:59 hôm qua, giờ Việt Nam
        var today = ClsBatches.StartOfDayUtc(DateTime.UtcNow);                // 00:00 hôm nay

        var unpaidPast = await ChargeAsync(db, price, "CLSService", "Pending", past, paid: 0m);
        var unpaidOld = await ChargeAsync(db, price, "CLSService", "Pending", past.AddDays(-3), paid: 0m);
        var paidPast = await ChargeAsync(db, price, "CLSService", "Paid", past, paid: 200_000m);
        var partlyPaidPast = await ChargeAsync(db, price, "CLSService", "PartiallyPaid", past, paid: 50_000m);
        var unpaidToday = await ChargeAsync(db, price, "CLSService", "Pending", today, paid: 0m);
        var examPast = await ChargeAsync(db, price, "ConsultationFee", "Pending", past, paid: 0m);

        var cancelled = await Service(db).CancelUnpaidAsync(Ct);

        Assert.True(cancelled >= 2);
        Assert.Equal(
            [$"{unpaidPast}|Cancelled", $"{unpaidOld}|Cancelled", $"{paidPast}|Paid", $"{partlyPaidPast}|PartiallyPaid",
             $"{unpaidToday}|Pending", $"{examPast}|Pending"],
            await StatusesAsync(db, unpaidPast, unpaidOld, paidPast, partlyPaidPast, unpaidToday, examPast));
    }

    [Fact]
    public async Task Running_again_changes_nothing()
    {
        await using var db = await OpenAsync();
        var price = await PriceItemAsync(db);
        var charge = await ChargeAsync(db, price, "CLSService", "Pending", ClsBatches.StartOfDayUtc(DateTime.UtcNow).AddHours(-5), paid: 0m);

        await Service(db).CancelUnpaidAsync(Ct);
        var second = await Service(db).CancelUnpaidAsync(Ct);

        Assert.Equal(0, second);
        Assert.Equal([$"{charge}|Cancelled"], await StatusesAsync(db, charge));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ClsDayEndService Service(TestDatabase db)
    {
        var billing = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        return new ClsDayEndService(new EfChargeStore(billing), new EfPaymentStore(billing, new OutboxWriter(shared, TimeProvider.System)), TimeProvider.System);
    }

    private static async Task<Guid> PriceItemAsync(TestDatabase db)
    {
        var list = await db.InsertAsync("billing.PriceList", new
        {
            PriceListCode = "T-" + Guid.NewGuid().ToString("N")[..10], Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-10),
        });
        return await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "CLSService", MedicalServiceId = Guid.NewGuid(), UnitPrice = 200_000m, IsActive = true });
    }

    private static Task<Guid> ChargeAsync(TestDatabase db, Guid priceItem, string type, string status, DateTime createdAtUtc, decimal paid) =>
        db.InsertAsync("billing.BillingItem", new
        {
            EncounterId = Guid.NewGuid(), ServiceOrderItemId = Guid.NewGuid(), ServiceOrderId = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            PriceListItemId = priceItem, ItemType = type, ItemNameSnapshot = "Khoản giả lập", UnitPrice = 200_000m, Quantity = 1,
            TotalAmount = 200_000m, PaidAmount = paid, Status = status, CreatedAtUtc = createdAtUtc,
        });

    private static async Task<List<string>> StatusesAsync(TestDatabase db, params Guid[] ids)
    {
        var rows = await db.QueryAsync($"""
            SELECT CONCAT(LOWER(CONVERT(varchar(36), Id)), '|', Status) FROM billing.BillingItem
            WHERE Id IN ({string.Join(", ", ids.Select(i => $"'{i}'"))})
            """);
        return ids.Select(id => rows.Single(r => r.StartsWith(id.ToString(), StringComparison.Ordinal))).ToList();
    }
}
