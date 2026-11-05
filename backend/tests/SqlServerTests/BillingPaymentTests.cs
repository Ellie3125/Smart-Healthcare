using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Chạy riêng, không song song với các class khác: cùng giữ transaction chưa commit trên các bảng billing của SmartHealthcare_Test.
[CollectionDefinition(nameof(BillingPaymentSerialCollection), DisableParallelization = true)]
public sealed class BillingPaymentSerialCollection;

// Tạo yêu cầu thanh toán và ghi nhận thanh toán đã xác minh trên SQL Server thật (contract v0.2). Dữ liệu giả lập, rollback sau mỗi test.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingPaymentTests
{
    [Fact]
    public async Task Exam_payment_is_created_once_per_booking_attempt()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var command = Exam(Guid.NewGuid(), charge, PaymentProvider.VNPAY);

        var first = await Payments(db).CreateAsync(command, Ct);
        var second = await Payments(db).CreateAsync(command, Ct);

        Assert.Equal(first.PaymentIntentId, second.PaymentIntentId);
        Assert.Equal(first.TransactionReference, second.TransactionReference);
        Assert.Equal(150_000m, first.Amount);
        Assert.Equal(PaymentState.Pending, first.Status);
        Assert.Equal(["1|1|150000.00|Pending"], await db.QueryAsync($"""
            SELECT (SELECT COUNT(*) FROM billing.PaymentIntent WHERE BookingAttemptId = '{command.BookingAttemptId}'),
                   COUNT(*), CONVERT(varchar(20), MAX(a.AllocatedAmount)), MAX(t.PaymentStatus)
            FROM billing.PaymentAllocation a JOIN billing.PaymentTransaction t ON t.Id = a.PaymentTransactionId
            WHERE a.BillingItemId = '{charge}'
            """));
    }

    [Fact]
    public async Task Another_provider_for_an_open_booking_payment_is_a_conflict()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var attempt = Guid.NewGuid();
        await Payments(db).CreateAsync(Exam(attempt, charge, PaymentProvider.VNPAY), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).CreateAsync(Exam(attempt, charge, PaymentProvider.MoMo), Ct));

        Assert.Equal(BillingErrorCode.PaymentConflict, error.Code);
    }

    [Fact]
    public async Task Cls_charge_in_an_open_payment_cannot_join_another_payment()
    {
        await using var db = await OpenAsync();
        var encounter = Guid.NewGuid();
        var ultrasound = await ClsChargeAsync(db, encounter, 200_000m);
        var xray = await ClsChargeAsync(db, encounter, 120_000m);
        var batch = await Payments(db).CreateAsync(Cls(encounter, [ultrasound, xray]), Ct);
        Assert.Equal(320_000m, batch.Amount);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).CreateAsync(Cls(encounter, [ultrasound]), Ct));

        Assert.Equal(BillingErrorCode.PaymentConflict, error.Code);
    }

    [Fact]
    public async Task Cls_charge_of_another_encounter_is_not_payable()
    {
        await using var db = await OpenAsync();
        var charge = await ClsChargeAsync(db, Guid.NewGuid(), 200_000m);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).CreateAsync(Cls(Guid.NewGuid(), [charge]), Ct));

        Assert.Equal(BillingErrorCode.ChargeNotPayable, error.Code);
    }

    [Fact]
    public async Task Verified_payment_marks_charge_paid_and_writes_PaymentVerified_to_outbox_once()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var attempt = Guid.NewGuid();
        var payment = await Payments(db).CreateAsync(Exam(attempt, charge, PaymentProvider.VNPAY), Ct);
        var ipn = new VerifiedPayment(payment.TransactionReference, "VNP-14001", 150_000m, "VND", DateTime.UtcNow, "{\"vnp_ResponseCode\":\"00\"}");

        var confirmed = await Payments(db).ConfirmVerifiedAsync(ipn, Ct);
        var repeated = await Payments(db).ConfirmVerifiedAsync(ipn, Ct);

        Assert.False(confirmed.AlreadyRecorded);
        Assert.Equal([charge], confirmed.PaidChargeIds);
        Assert.True(repeated.AlreadyRecorded);
        Assert.Equal(["Paid|150000.00|Success|VNP-14001|Succeeded"], await db.QueryAsync($"""
            SELECT b.Status, CONVERT(varchar(20), b.PaidAmount), t.PaymentStatus, t.ProviderTransactionId, i.Status
            FROM billing.BillingItem b, billing.PaymentTransaction t, billing.PaymentIntent i
            WHERE b.Id = '{charge}' AND t.TransactionReference = '{payment.TransactionReference}' AND i.Id = '{payment.PaymentIntentId}'
            """));

        var outbox = await db.QueryAsync($"SELECT EventType, AggregateType, Status, PayloadJson FROM shared.OutboxMessage WHERE AggregateId = '{payment.PaymentIntentId}'");
        var row = Assert.Single(outbox);
        Assert.StartsWith("PaymentVerified|PaymentIntent|Pending|", row);
        Assert.Contains(charge.ToString(), row);
        Assert.Contains(attempt.ToString(), row);
        Assert.Contains("\"Purpose\":\"InitialConsultation\"", row);
        Assert.Contains("\"Version\":1", row);
    }

    [Fact]
    public async Task Amount_mismatch_records_nothing()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), charge, PaymentProvider.MoMo), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).ConfirmVerifiedAsync(
            new VerifiedPayment(payment.TransactionReference, "MOMO-1", 100_000m, "VND", DateTime.UtcNow, null), Ct));

        Assert.Equal(BillingErrorCode.AmountMismatch, error.Code);
        Assert.Equal(["Pending|Pending|0"], await db.QueryAsync($"""
            SELECT b.Status, t.PaymentStatus, (SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{payment.PaymentIntentId}')
            FROM billing.BillingItem b, billing.PaymentTransaction t
            WHERE b.Id = '{charge}' AND t.TransactionReference = '{payment.TransactionReference}'
            """));
    }

    [Fact]
    public async Task Provider_transaction_id_already_used_by_another_payment_is_a_conflict()
    {
        await using var db = await OpenAsync();
        var first = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db), PaymentProvider.VNPAY), Ct);
        var second = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db), PaymentProvider.VNPAY), Ct);
        await Payments(db).ConfirmVerifiedAsync(new VerifiedPayment(first.TransactionReference, "VNP-SAME", 150_000m, "VND", DateTime.UtcNow, null), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).ConfirmVerifiedAsync(
            new VerifiedPayment(second.TransactionReference, "VNP-SAME", 150_000m, "VND", DateTime.UtcNow, null), Ct));

        Assert.Equal(BillingErrorCode.PaymentConflict, error.Code);
        Assert.Equal(["Pending|0"], await db.QueryAsync($"""
            SELECT t.PaymentStatus, (SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{second.PaymentIntentId}')
            FROM billing.PaymentTransaction t WHERE t.TransactionReference = '{second.TransactionReference}'
            """));
    }

    [Fact]
    public async Task Paid_charge_is_not_payable_again()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var attempt = Guid.NewGuid();
        var payment = await Payments(db).CreateAsync(Exam(attempt, charge, PaymentProvider.VNPAY), Ct);
        await Payments(db).ConfirmVerifiedAsync(new VerifiedPayment(payment.TransactionReference, "VNP-2", 150_000m, "VND", DateTime.UtcNow, null), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).CreateAsync(Exam(attempt, charge, PaymentProvider.VNPAY), Ct));

        Assert.Equal(BillingErrorCode.ChargeNotPayable, error.Code);
    }

    [Fact]
    public async Task Unknown_transaction_reference_is_reported()
    {
        await using var db = await OpenAsync();

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).ConfirmVerifiedAsync(
            new VerifiedPayment("NO-SUCH-REF", "VNP-3", 1m, "VND", DateTime.UtcNow, null), Ct));

        Assert.Equal(BillingErrorCode.PaymentNotFound, error.Code);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Mỗi lần gọi một cặp DbContext mới (Billing + Shared) trên cùng kết nối và transaction của test.
    private static PaymentService Payments(TestDatabase db)
    {
        var billing = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        return new PaymentService(new EfPaymentStore(billing, new OutboxWriter(shared, TimeProvider.System)), TimeProvider.System);
    }

    private static CreatePayment Exam(Guid attempt, Guid charge, PaymentProvider provider) =>
        new(PaymentPurpose.InitialConsultation, Guid.NewGuid(), Guid.NewGuid(), attempt, null, [charge], provider, DateTime.UtcNow.AddMinutes(5));

    private static CreatePayment Cls(Guid encounter, Guid[] charges) =>
        new(PaymentPurpose.CLSOrderBatch, Guid.NewGuid(), Guid.NewGuid(), null, encounter, charges, PaymentProvider.MoMo, DateTime.UtcNow.AddMinutes(30));

    private static async Task<Guid> ExamChargeAsync(TestDatabase db)
    {
        var list = await db.InsertAsync("billing.PriceList", new { Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1) });
        var item = await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "ConsultationFee", DoctorRankId = Guid.NewGuid(), UnitPrice = 150_000m, IsActive = true });
        return await db.InsertAsync("billing.BillingItem", new
        {
            PriceListItemId = item, ItemType = "ConsultationFee", ItemNameSnapshot = "Phí khám giả lập", UnitPrice = 150_000m, Quantity = 1, TotalAmount = 150_000m, Status = "Pending",
        });
    }

    private static async Task<Guid> ClsChargeAsync(TestDatabase db, Guid encounter, decimal price)
    {
        var list = await db.InsertAsync("billing.PriceList", new { Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1) });
        var item = await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = "CLSService", MedicalServiceId = Guid.NewGuid(), UnitPrice = price, IsActive = true });
        return await db.InsertAsync("billing.BillingItem", new
        {
            EncounterId = encounter, ServiceOrderItemId = Guid.NewGuid(), PriceListItemId = item, ItemType = "CLSService",
            ItemNameSnapshot = "Dịch vụ CLS giả lập", UnitPrice = price, Quantity = 1, TotalAmount = price, Status = "Pending",
        });
    }
}
