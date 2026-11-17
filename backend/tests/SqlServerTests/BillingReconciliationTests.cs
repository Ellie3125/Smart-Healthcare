using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Đối soát VNPAY (querydr) và hết hạn yêu cầu thanh toán trên SQL Server thật, với cổng giả. Dữ liệu giả lập, rollback sau mỗi test.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingReconciliationTests
{
    [Fact]
    public async Task Paid_at_gateway_is_recorded_like_ipn_and_then_final()
    {
        await using var db = await OpenAsync();
        var (payment, charge) = await OverduePaymentAsync(db, 200_000m, minutesOverdue: 5);
        var gateway = new FakeGateway(GatewayPaymentState.Paid, "VNP-RECON-1", 200_000m);

        var first = await Service(db, gateway).ReconcileAsync(payment.TransactionReference, Ct);
        var again = await Service(db, gateway).ReconcileAsync(payment.TransactionReference, Ct);

        Assert.Equal((ReconcileOutcome.PaidRecorded, PaymentState.Succeeded, "VNP-RECON-1"), (first.Outcome, first.Status, first.ProviderTransactionId));
        Assert.Equal(ReconcileOutcome.AlreadyFinal, again.Outcome);
        Assert.Equal(1, gateway.Calls);
        Assert.Equal(["Paid"], await db.QueryAsync($"SELECT Status FROM billing.BillingItem WHERE Id = '{charge}'"));
        Assert.Equal(["PaymentVerified"], await db.QueryAsync($"SELECT EventType FROM shared.OutboxMessage WHERE AggregateId = '{payment.PaymentIntentId}'"));
    }

    [Fact]
    public async Task Overdue_without_gateway_transaction_expires_but_late_ipn_can_still_record()
    {
        await using var db = await OpenAsync();
        var (payment, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 5);

        var result = await Service(db, new FakeGateway(GatewayPaymentState.NotFound)).ReconcileAsync(payment.TransactionReference, Ct);

        Assert.Equal((ReconcileOutcome.Expired, PaymentState.Expired), (result.Outcome, result.Status));
        Assert.Equal(["Expired|Pending"], await StatusAsync(db, payment));

        // Giao dịch vẫn Pending: cổng gửi IPN thành công muộn thì tiền vẫn được ghi nhận.
        await Payments(db).ConfirmVerifiedAsync(new VerifiedPayment(payment.TransactionReference, "VNP-LATE", "VNP-LATE", 150_000m, "VND",
            DateTime.UtcNow, "{}"), Ct);
        Assert.Equal(["Succeeded|Success"], await StatusAsync(db, payment));
    }

    [Fact]
    public async Task Failed_at_gateway_is_recorded_as_failed()
    {
        await using var db = await OpenAsync();
        var (payment, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 5);

        var result = await Service(db, new FakeGateway(GatewayPaymentState.Failed, "VNP-FAIL-1")).ReconcileAsync(payment.TransactionReference, Ct);

        Assert.Equal(ReconcileOutcome.FailedRecorded, result.Outcome);
        Assert.Equal(["Failed|Failed"], await StatusAsync(db, payment));
    }

    [Fact]
    public async Task Long_pending_reversal_or_amount_mismatch_goes_to_review_list()
    {
        await using var db = await OpenAsync();
        var (stuck, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 40);
        var (reversal, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 5);
        var (mismatch, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 5);
        var (recentPending, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 5);

        var stuckResult = await Service(db, new FakeGateway(GatewayPaymentState.Pending)).ReconcileAsync(stuck.TransactionReference, Ct);
        var reversalResult = await Service(db, new FakeGateway(GatewayPaymentState.NeedsReview)).ReconcileAsync(reversal.TransactionReference, Ct);
        var mismatchResult = await Service(db, new FakeGateway(GatewayPaymentState.Paid, "VNP-MISMATCH", 1m)).ReconcileAsync(mismatch.TransactionReference, Ct);
        var recentResult = await Service(db, new FakeGateway(GatewayPaymentState.Pending)).ReconcileAsync(recentPending.TransactionReference, Ct);

        Assert.Equal([ReconcileOutcome.NeedsReview, ReconcileOutcome.NeedsReview, ReconcileOutcome.NeedsReview, ReconcileOutcome.StillPending],
            [stuckResult.Outcome, reversalResult.Outcome, mismatchResult.Outcome, recentResult.Outcome]);
        Assert.Equal(["Unknown|Pending"], await StatusAsync(db, mismatch));
        var review = await Service(db, new FakeGateway(GatewayPaymentState.Pending)).ListNeedingReviewAsync(Ct);
        Assert.Superset(new HashSet<string> { stuck.TransactionReference, reversal.TransactionReference, mismatch.TransactionReference },
            review.Select(r => r.TransactionReference).ToHashSet());
        Assert.DoesNotContain(review, r => r.TransactionReference == recentPending.TransactionReference);
    }

    [Fact]
    public async Task Gateway_errors_and_missing_configuration_change_nothing_until_review_time()
    {
        await using var db = await OpenAsync();
        var (recent, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 5);
        var (old, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 40);

        var unconfigured = await Service(db, new FakeGateway(GatewayPaymentState.Paid) { Configured = false }).ReconcileAsync(old.TransactionReference, Ct);
        var failing = await Service(db, new FakeGateway(GatewayPaymentState.Paid) { Throws = true }).ReconcileAsync(recent.TransactionReference, Ct);
        var failingOld = await Service(db, new FakeGateway(GatewayPaymentState.Paid) { Throws = true }).ReconcileAsync(old.TransactionReference, Ct);

        Assert.Equal(ReconcileOutcome.GatewayUnavailable, unconfigured.Outcome);
        Assert.Equal((ReconcileOutcome.GatewayUnavailable, "VNPAY querydr từ chối: 99 Unexpected error"), (failing.Outcome, failing.Detail));
        Assert.Equal(["Pending|Pending"], await StatusAsync(db, recent));
        Assert.Equal(ReconcileOutcome.NeedsReview, failingOld.Outcome);
    }

    [Fact]
    public async Task Job_only_picks_requests_past_the_grace_period()
    {
        await using var db = await OpenAsync();
        var (due, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 5);
        var (inGrace, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: 1);
        var (open, _) = await OverduePaymentAsync(db, 150_000m, minutesOverdue: -10);

        await Service(db, new FakeGateway(GatewayPaymentState.NotFound)).ReconcileOverdueAsync(Ct);

        Assert.Equal(["Expired|Pending"], await StatusAsync(db, due));
        Assert.Equal(["Pending|Pending"], await StatusAsync(db, inGrace));
        Assert.Equal(["Pending|Pending"], await StatusAsync(db, open));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<List<string>> StatusAsync(TestDatabase db, PaymentDto payment) => db.QueryAsync($"""
        SELECT CONCAT(i.Status, '|', t.PaymentStatus) FROM billing.PaymentIntent i JOIN billing.PaymentTransaction t ON t.PaymentIntentId = i.Id
        WHERE t.TransactionReference = '{payment.TransactionReference}'
        """);

    // Yêu cầu thanh toán phí CLS qua VNPAY, rồi lùi giờ tạo và hạn để giả lập đã quá hạn minutesOverdue phút (âm: còn hạn).
    private static async Task<(PaymentDto Payment, Guid Charge)> OverduePaymentAsync(TestDatabase db, decimal price, int minutesOverdue)
    {
        var list = await db.InsertAsync("billing.PriceList", new
        {
            PriceListCode = "T-" + Guid.NewGuid().ToString("N")[..10], Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
        });
        var item = await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "CLSService", MedicalServiceId = Guid.NewGuid(), UnitPrice = price, IsActive = true });
        var encounter = Guid.NewGuid();
        var charge = await db.InsertAsync("billing.BillingItem", new
        {
            EncounterId = encounter, ServiceOrderItemId = Guid.NewGuid(), ServiceOrderId = Guid.NewGuid(), PatientId = Guid.NewGuid(),
            PriceListItemId = item, ItemType = "CLSService", ItemNameSnapshot = "Dịch vụ CLS giả lập", UnitPrice = price, Quantity = 1,
            TotalAmount = price, Status = "Pending", CreatedAtUtc = DateTime.UtcNow,
        });
        var payment = await Payments(db).CreateAsync(new CreatePayment(PaymentPurpose.CLSOrderBatch, Guid.NewGuid(), Guid.NewGuid(), null, encounter,
            [charge], PaymentProvider.VNPAY, DateTime.UtcNow.AddMinutes(15)), Ct);
        await db.QueryAsync($"""
            UPDATE billing.PaymentIntent
            SET CreatedAtUtc = DATEADD(minute, {-minutesOverdue - 15}, SYSUTCDATETIME()), ExpiresAtUtc = DATEADD(minute, {-minutesOverdue}, SYSUTCDATETIME())
            WHERE Id = '{payment.PaymentIntentId}';
            SELECT 1;
            """);
        return (payment, charge);
    }

    private static (BillingDbContext Billing, EfPaymentStore Store) Contexts(TestDatabase db)
    {
        var billing = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        return (billing, new EfPaymentStore(billing, new OutboxWriter(shared, TimeProvider.System)));
    }

    private static PaymentService Payments(TestDatabase db) => new(Contexts(db).Store, TimeProvider.System);

    private static PaymentReconciliationService Service(TestDatabase db, IPaymentStatusGateway gateway)
    {
        var (billing, store) = Contexts(db);
        return new PaymentReconciliationService(new EfReconciliationStore(billing), store, new PaymentService(store, TimeProvider.System), gateway,
            TimeProvider.System);
    }

    private sealed class FakeGateway(GatewayPaymentState state, string? providerTransactionId = null, decimal? amount = null) : IPaymentStatusGateway
    {
        public bool Configured { get; init; } = true;
        public bool Throws { get; init; }
        public int Calls { get; private set; }

        public bool IsConfigured => Configured;

        public Task<GatewayPaymentStatus> QueryAsync(GatewayStatusQuery query, CancellationToken ct)
        {
            Calls++;
            if (Throws)
                throw new PaymentGatewayException("VNPAY querydr từ chối: 99 Unexpected error");
            return Task.FromResult(new GatewayPaymentStatus(state, providerTransactionId, amount, DateTime.UtcNow, "xx", "{}"));
        }
    }
}
