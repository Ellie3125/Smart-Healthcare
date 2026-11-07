using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Truy vấn bảng giá và khoản thu của Billing (EfPriceCatalog, EfChargeStore) chạy trên SQL Server thật,
// qua BillingModule; mọi dữ liệu nằm trong transaction của test và bị rollback. Giá là giả lập.
// Chạy riêng, không song song với ConstraintTests: cả hai giữ transaction chưa commit trên billing.PriceList/PriceListItem
// của cùng DB SmartHealthcare_Test; chạy cùng lúc thì truy vấn bảng giá chờ khóa của nhau (timeout, deadlock).
[CollectionDefinition(nameof(BillingQuerySerialCollection), DisableParallelization = true)]
public sealed class BillingQuerySerialCollection;

[Collection(nameof(BillingQuerySerialCollection))]
public class BillingQueryTests
{
    private static readonly DateTime From = new(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cut = new(2027, 1, 31, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime At = new(2027, 1, 15, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Exam_fee_query_filters_inactive_expired_and_non_VND_and_prefers_doctor_price()
    {
        await using var db = await OpenAsync();
        var rank = Guid.NewGuid();
        var doctor = Guid.NewGuid();

        var current = await PriceListAsync(db, From, null);
        var rankItem = await FeeAsync(db, current, rank: rank, price: 150_000m);
        await FeeAsync(db, current, rank: rank, price: 1m, active: false);
        await FeeAsync(db, await PriceListAsync(db, From.AddYears(-1), From), rank: rank, price: 2m);
        await FeeAsync(db, await PriceListAsync(db, From, null, currency: "USD"), rank: rank, price: 3m);
        await FeeAsync(db, await PriceListAsync(db, From, null, active: false), rank: rank, price: 4m);

        var module = Module(db);

        var byRank = await module.QuoteExamFeeAsync(new QuoteExamFee(Guid.NewGuid(), rank, At), Ct);
        Assert.Equal(rankItem, byRank.PriceListItemId);
        Assert.Equal(150_000m, byRank.UnitPrice);
        Assert.Equal(PriceBasis.DoctorRank, byRank.Basis);

        var doctorItem = await FeeAsync(db, current, doctor: doctor, price: 250_000m);
        var byDoctor = await module.QuoteExamFeeAsync(new QuoteExamFee(doctor, rank, At), Ct);
        Assert.Equal(doctorItem, byDoctor.PriceListItemId);
        Assert.Equal(PriceBasis.Doctor, byDoctor.Basis);
    }

    [Fact]
    public async Task Effective_to_is_exclusive_in_the_database_query()
    {
        await using var db = await OpenAsync();
        var service = Guid.NewGuid();
        await FeeAsync(db, await PriceListAsync(db, From, Cut), service: service, price: 200_000m);
        await FeeAsync(db, await PriceListAsync(db, Cut, null), service: service, price: 250_000m);
        var module = Module(db);

        Assert.Equal(200_000m, (await module.QuoteClsFeesAsync(new QuoteClsFees([service], Cut.AddMilliseconds(-1)), Ct))[0].UnitPrice);
        Assert.Equal(250_000m, (await module.QuoteClsFeesAsync(new QuoteClsFees([service], Cut), Ct))[0].UnitPrice);
    }

    [Fact]
    public async Task Cls_charges_are_saved_with_snapshot_and_satisfy_db_checks_and_are_not_duplicated()
    {
        await using var db = await OpenAsync();
        var ultrasound = Guid.NewGuid();
        var xray = Guid.NewGuid();
        var list = await PriceListAsync(db, From, null);
        await FeeAsync(db, list, service: ultrasound, price: 200_000m);
        await FeeAsync(db, list, service: xray, price: 120_000m);
        var encounter = Guid.NewGuid();
        var command = new CreateClsCharges(encounter, At,
        [
            new ClsChargeLine(Guid.NewGuid(), ultrasound, "Siêu âm ổ bụng", 1),
            new ClsChargeLine(Guid.NewGuid(), xray, "X-quang ngực", 2),
        ]);

        var first = await Module(db).CreateClsChargesAsync(command, Ct);
        // Context mới: đọc lại từ DB, không dùng change tracker của lần gọi trước.
        var second = await Module(db).CreateClsChargesAsync(command, Ct);

        Assert.Equal(first.Select(c => c.ChargeId), second.Select(c => c.ChargeId));
        // CONVERT sang chuỗi trong SQL để không phụ thuộc culture của máy chạy test.
        Assert.Equal(["Siêu âm ổ bụng|200000.00|1|200000.00|Pending", "X-quang ngực|120000.00|2|240000.00|Pending"], await db.QueryAsync(
            $"""
            SELECT ItemNameSnapshot, CONVERT(varchar(20), UnitPrice), Quantity, CONVERT(varchar(20), TotalAmount), Status
            FROM billing.BillingItem WHERE EncounterId = '{encounter}' ORDER BY UnitPrice DESC
            """));

        var summary = await Module(db).GetEncounterCostSummaryAsync(encounter, Ct);
        Assert.Equal(440_000m, summary.TotalPayable);
        Assert.Equal(440_000m, summary.Outstanding);
        Assert.Equal(0m, summary.TotalPaid);
    }

    [Fact]
    public async Task Duplicate_active_charges_in_the_database_are_reported()
    {
        await using var db = await OpenAsync();
        var service = Guid.NewGuid();
        var item = await FeeAsync(db, await PriceListAsync(db, From, null), service: service, price: 200_000m);
        var encounter = Guid.NewGuid();
        var serviceOrderItem = Guid.NewGuid();
        for (var i = 0; i < 2; i++)
        {
            await db.InsertAsync("billing.BillingItem", new
            {
                EncounterId = encounter, ServiceOrderItemId = serviceOrderItem, PriceListItemId = item, ItemType = "CLSService",
                UnitPrice = 200_000m, Quantity = 1, TotalAmount = 200_000m, Status = "Pending",
            });
        }

        var error = await Assert.ThrowsAsync<BillingException>(() => Module(db).CreateClsChargesAsync(
            new CreateClsCharges(encounter, At, [new ClsChargeLine(serviceOrderItem, service, "Siêu âm", 1)]), Ct));

        Assert.Equal(BillingErrorCode.DuplicateCharge, error.Code);
        Assert.Equal(["2"], await db.QueryAsync($"SELECT COUNT(*) FROM billing.BillingItem WHERE ServiceOrderItemId = '{serviceOrderItem}'"));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Mỗi lần gọi một DbContext mới trên cùng transaction; context không sở hữu kết nối của test.
    private static BillingModule Module(TestDatabase db)
    {
        var context = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        var payments = new PaymentService(new EfPaymentStore(context, new OutboxWriter(shared, TimeProvider.System)), TimeProvider.System);
        return new BillingModule(new EfPriceCatalog(context), new EfChargeStore(context), payments, TimeProvider.System);
    }

    private static Task<Guid> PriceListAsync(TestDatabase db, DateTime from, DateTime? to, string currency = "VND", bool active = true) =>
        db.InsertAsync("billing.PriceList", new
        {
            PriceListCode = "T-" + Guid.NewGuid().ToString("N")[..10], Currency = currency, EffectiveFromUtc = from, EffectiveToUtc = to, IsActive = active,
        });

    private static Task<Guid> FeeAsync(TestDatabase db, Guid priceList, Guid? rank = null, Guid? doctor = null, Guid? service = null, decimal price = 0m, bool active = true) =>
        db.InsertAsync("billing.PriceListItem", new
        {
            PriceListId = priceList, ItemType = service is null ? "ConsultationFee" : "CLSService",
            DoctorRankId = rank, DoctorId = doctor, MedicalServiceId = service, UnitPrice = price, IsActive = active,
        });
}
