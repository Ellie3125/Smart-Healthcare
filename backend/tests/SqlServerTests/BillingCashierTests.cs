using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Thu tiền mặt tại quầy cho đợt CLS trên SQL Server thật. Dữ liệu giả lập, rollback sau mỗi test.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingCashierTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();

    [Fact]
    public async Task Cash_collection_pays_the_batch_and_writes_receipt_outbox_and_audit_together()
    {
        await using var db = await OpenAsync();
        var (order, encounter, charges) = await BatchAsync(db, 200_000m, 90_000m);

        var result = await Cashier(db).CollectClsAsync(order, Staff, "10.0.0.5", Ct);

        Assert.Equal(CashCollectionOutcome.Collected, result.Outcome);
        var receipt = result.Receipt!;
        Assert.Equal(290_000m, receipt.Amount);
        Assert.StartsWith("PT", receipt.ReceiptNumber);
        Assert.Equal((Patient, encounter, order), (receipt.PatientId, receipt.EncounterId, receipt.ServiceOrderId));
        Assert.Equal(["Paid", "Paid"], await db.QueryAsync(
            $"SELECT Status FROM billing.BillingItem WHERE Id IN ('{charges[0]}', '{charges[1]}')"));
        Assert.Equal([$"Counter|Succeeded|CLSOrderBatch|{Staff}|Success|290000.00".ToUpperInvariant()], await db.QueryAsync($"""
            SELECT UPPER(CONCAT(i.Provider, '|', i.Status, '|', i.PaymentType, '|', i.PayerAppUserId, '|', t.PaymentStatus, '|', CONVERT(varchar(20), t.TransactionAmount)))
            FROM billing.PaymentIntent i JOIN billing.PaymentTransaction t ON t.PaymentIntentId = i.Id
            WHERE t.TransactionReference = '{receipt.ReceiptNumber}'
            """));
        Assert.Equal(["PaymentVerified"], await db.QueryAsync(
            $"SELECT EventType FROM shared.OutboxMessage WHERE AggregateId = '{receipt.PaymentIntentId}'"));
        Assert.Equal([$"{Staff}|INSERT|10.0.0.5".ToUpperInvariant()], await db.QueryAsync($"""
            SELECT UPPER(CONCAT(a.PerformedByUserId, '|', a.ActionType, '|', a.ClientIpAddress))
            FROM shared.AuditLog a JOIN billing.PaymentTransaction t ON t.Id = a.RecordId
            WHERE t.TransactionReference = '{receipt.ReceiptNumber}'
            """));
    }

    [Fact]
    public async Task Open_online_request_is_cancelled_and_second_collection_has_nothing_to_pay()
    {
        await using var db = await OpenAsync();
        var (order, encounter, charges) = await BatchAsync(db, 200_000m);
        var online = await Payments(db).CreateAsync(new CreatePayment(PaymentPurpose.CLSOrderBatch, Patient, Guid.NewGuid(), null, encounter,
            charges, PaymentProvider.VNPAY, DateTime.UtcNow.AddMinutes(15)), Ct);

        var first = await Cashier(db).CollectClsAsync(order, Staff, null, Ct);
        var second = await Cashier(db).CollectClsAsync(order, Staff, null, Ct);

        Assert.Equal(1, first.Receipt!.CancelledOnlineRequests);
        Assert.Equal(["Cancelled"], await db.QueryAsync($"SELECT Status FROM billing.PaymentIntent WHERE Id = '{online.PaymentIntentId}'"));
        Assert.Equal(CashCollectionOutcome.NothingToPay, second.Outcome);
        Assert.Equal(["1"], await db.QueryAsync($"""
            SELECT COUNT(*) FROM billing.PaymentIntent WHERE EncounterId = '{encounter}' AND Provider = 'Counter'
            """));
    }

    [Fact]
    public async Task Shift_summary_counts_only_this_cashier_today_and_list_puts_unpaid_first()
    {
        await using var db = await OpenAsync();
        var (paidOrder, _, _) = await BatchAsync(db, 200_000m);
        var (otherCashierOrder, _, _) = await BatchAsync(db, 150_000m);
        var (unpaidOrder, _, _) = await BatchAsync(db, 90_000m);
        await Cashier(db).CollectClsAsync(paidOrder, Staff, null, Ct);
        await Cashier(db).CollectClsAsync(otherCashierOrder, Guid.NewGuid(), null, Ct);

        var summary = await Cashier(db).GetTodaySummaryAsync(Staff, Ct);
        var list = await Cashier(db).ListTodayAsync(Patient, Ct);

        Assert.Equal((1, 200_000m, 200_000m), (summary.ReceiptCount, summary.CashCollected, summary.NetCash));
        Assert.Equal(unpaidOrder, list[0].ServiceOrderId);
        Assert.Equal(ClsBatchStatus.PendingPayment, list[0].Status);
        Assert.Equal(90_000m, list[0].Outstanding);
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public async Task Unknown_batch_is_not_found()
    {
        await using var db = await OpenAsync();
        Assert.Equal(CashCollectionOutcome.NotFound, (await Cashier(db).CollectClsAsync(Guid.NewGuid(), Staff, null, Ct)).Outcome);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Một đợt CLS của Patient, mỗi giá một khoản; tạo qua BillingModule như Clinical sẽ gọi.
    private static async Task<(Guid Order, Guid Encounter, Guid[] Charges)> BatchAsync(TestDatabase db, params decimal[] prices)
    {
        var list = await db.InsertAsync("billing.PriceList", new
        {
            PriceListCode = "T-" + Guid.NewGuid().ToString("N")[..10], Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
        });
        var lines = new List<ClsChargeLine>();
        foreach (var price in prices)
        {
            var service = Guid.NewGuid();
            await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "CLSService", MedicalServiceId = service, UnitPrice = price, IsActive = true });
            lines.Add(new ClsChargeLine(Guid.NewGuid(), service, "Dịch vụ CLS giả lập", 1));
        }

        var order = Guid.NewGuid();
        var encounter = Guid.NewGuid();
        var (billing, store) = Contexts(db);
        var module = new BillingModule(new EfPriceCatalog(billing), new EfChargeStore(billing), new PaymentService(store, TimeProvider.System), TimeProvider.System);
        var charges = await module.CreateClsChargesAsync(new CreateClsCharges(encounter, Patient, order, DateTime.UtcNow, lines), Ct);
        return (order, encounter, charges.Select(c => c.ChargeId).ToArray());
    }

    // Billing, Shared (Outbox) và Shared (AuditLog) dùng chung một SharedDbContext như trong DI.
    private static (BillingDbContext Billing, EfPaymentStore Store, SharedDbContext Shared) ContextsWithShared(TestDatabase db)
    {
        var billing = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        return (billing, new EfPaymentStore(billing, new OutboxWriter(shared, TimeProvider.System)), shared);
    }

    private static (BillingDbContext Billing, EfPaymentStore Store) Contexts(TestDatabase db)
    {
        var (billing, store, _) = ContextsWithShared(db);
        return (billing, store);
    }

    private static PaymentService Payments(TestDatabase db) => new(Contexts(db).Store, TimeProvider.System);

    private static CashierService Cashier(TestDatabase db)
    {
        var (billing, store, shared) = ContextsWithShared(db);
        return new CashierService(new EfChargeStore(billing), store, new EfCashierStore(billing, shared, TimeProvider.System), TimeProvider.System);
    }
}
