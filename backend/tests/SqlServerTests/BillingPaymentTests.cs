using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Gateway;
using Billing.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Chạy riêng, không song song với các class khác: cùng giữ transaction chưa commit trên các bảng billing của SmartHealthcare_Test.
[CollectionDefinition(nameof(BillingPaymentSerialCollection), DisableParallelization = true)]
public sealed class BillingPaymentSerialCollection;

// Tạo yêu cầu thanh toán và ghi nhận kết quả VNPAY trên SQL Server thật. Dữ liệu giả lập, rollback sau mỗi test.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingPaymentTests
{
    private const string TmnCode = "TESTCODE";
    private const string Secret = "sql-test-secret-0123456789abcdef012345";

    [Fact]
    public async Task Exam_payment_is_created_once_per_booking_attempt()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var command = Exam(Guid.NewGuid(), charge);

        var first = await Payments(db).CreateAsync(command, Ct);
        var second = await Payments(db).CreateAsync(command, Ct);

        Assert.Equal(first.PaymentIntentId, second.PaymentIntentId);
        Assert.Equal(first.TransactionReference, second.TransactionReference);
        Assert.Equal(150_000m, first.Amount);
        Assert.Equal(PaymentState.Pending, first.Status);
        Assert.Equal(PaymentProvider.VNPAY, first.Provider);
        Assert.Equal(["1|1|150000.00|Pending|VNPAY"], await db.QueryAsync($"""
            SELECT (SELECT COUNT(*) FROM billing.PaymentIntent WHERE BookingAttemptId = '{command.BookingAttemptId}'),
                   COUNT(*), CONVERT(varchar(20), MAX(a.AllocatedAmount)), MAX(t.PaymentStatus),
                   (SELECT Provider FROM billing.PaymentIntent WHERE Id = '{first.PaymentIntentId}')
            FROM billing.PaymentAllocation a JOIN billing.PaymentTransaction t ON t.Id = a.PaymentTransactionId
            WHERE a.BillingItemId = '{charge}'
            """));
    }

    [Fact]
    public async Task Other_charges_for_an_open_booking_payment_are_a_conflict()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        await Payments(db).CreateAsync(Exam(attempt, await ExamChargeAsync(db)), Ct);
        var otherCharge = await ExamChargeAsync(db);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).CreateAsync(Exam(attempt, otherCharge), Ct));

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
    public async Task Verified_payment_marks_charge_paid_records_the_gateway_event_and_writes_PaymentVerified_once()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var attempt = Guid.NewGuid();
        var payment = await Payments(db).CreateAsync(Exam(attempt, charge), Ct);
        var result = Verified(payment, "VNP-14001", "EVT-14001");

        var confirmed = await Payments(db).ConfirmVerifiedAsync(result, Ct);
        var repeated = await Payments(db).ConfirmVerifiedAsync(result, Ct);

        Assert.False(confirmed.AlreadyRecorded);
        Assert.Equal([charge], confirmed.PaidChargeIds);
        Assert.True(repeated.AlreadyRecorded);
        Assert.Equal(["Paid|150000.00|Success|VNP-14001|Succeeded"], await db.QueryAsync($"""
            SELECT b.Status, CONVERT(varchar(20), b.PaidAmount), t.PaymentStatus, t.ProviderTransactionId, i.Status
            FROM billing.BillingItem b, billing.PaymentTransaction t, billing.PaymentIntent i
            WHERE b.Id = '{charge}' AND t.TransactionReference = '{payment.TransactionReference}' AND i.Id = '{payment.PaymentIntentId}'
            """));
        Assert.Equal(["VNPAY|EVT-14001|PaymentSuccess|Processed"], await db.QueryAsync($"""
            SELECT e.Provider, e.EventId, e.EventType, e.Status
            FROM billing.PaymentEvent e JOIN billing.PaymentTransaction t ON t.Id = e.PaymentTransactionId
            WHERE t.TransactionReference = '{payment.TransactionReference}'
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
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), charge), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).ConfirmVerifiedAsync(
            Verified(payment, "VNP-1", "EVT-1") with { Amount = 100_000m }, Ct));

        Assert.Equal(BillingErrorCode.AmountMismatch, error.Code);
        Assert.Equal(["Pending|Pending|0|0"], await db.QueryAsync($"""
            SELECT b.Status, t.PaymentStatus,
                   (SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{payment.PaymentIntentId}'),
                   (SELECT COUNT(*) FROM billing.PaymentEvent WHERE PaymentTransactionId = t.Id)
            FROM billing.BillingItem b, billing.PaymentTransaction t
            WHERE b.Id = '{charge}' AND t.TransactionReference = '{payment.TransactionReference}'
            """));
    }

    [Fact]
    public async Task Provider_transaction_id_already_used_by_another_payment_is_a_conflict()
    {
        await using var db = await OpenAsync();
        var first = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);
        var second = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);
        await Payments(db).ConfirmVerifiedAsync(Verified(first, "VNP-SAME", "EVT-A"), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).ConfirmVerifiedAsync(Verified(second, "VNP-SAME", "EVT-B"), Ct));

        Assert.Equal(BillingErrorCode.PaymentConflict, error.Code);
        Assert.Equal(["Pending|0"], await db.QueryAsync($"""
            SELECT t.PaymentStatus, (SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{second.PaymentIntentId}')
            FROM billing.PaymentTransaction t WHERE t.TransactionReference = '{second.TransactionReference}'
            """));
    }

    [Fact]
    public async Task Result_must_carry_the_gateway_transaction_already_recorded()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);
        // PaymentCheckoutService gắn mã này sau khi gọi cổng; nó từ chối chạy trong transaction nên test gắn trực tiếp.
        await db.QueryAsync($"UPDATE billing.PaymentTransaction SET ProviderTransactionId = 'VNP-ATTACHED' WHERE TransactionReference = '{payment.TransactionReference}'");

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).ConfirmVerifiedAsync(Verified(payment, "VNP-OTHER", "EVT-1"), Ct));
        var confirmed = await Payments(db).ConfirmVerifiedAsync(Verified(payment, "VNP-ATTACHED", "EVT-2"), Ct);

        Assert.Equal(BillingErrorCode.PaymentConflict, error.Code);
        Assert.False(confirmed.AlreadyRecorded);
    }

    [Fact]
    public async Task Failure_marks_the_request_failed_and_the_charge_can_be_paid_with_a_new_request()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var command = Exam(Guid.NewGuid(), charge);
        var payment = await Payments(db).CreateAsync(command, Ct);

        var failed = await Payments(db).RecordFailedAsync(Failed(payment, "VNP-F1", "EVT-F1"), Ct);
        var again = await Payments(db).RecordFailedAsync(Failed(payment, "VNP-F1", "EVT-F1"), Ct);
        var retry = await Payments(db).CreateAsync(command, Ct);

        Assert.Equal(PaymentFailureOutcome.Recorded, failed.Outcome);
        Assert.Equal(PaymentFailureOutcome.AlreadyRecorded, again.Outcome);
        Assert.Equal(["Failed|Failed|VNP-F1|Pending|0"], await db.QueryAsync($"""
            SELECT t.PaymentStatus, i.Status, t.ProviderTransactionId, b.Status,
                   (SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{payment.PaymentIntentId}')
            FROM billing.PaymentTransaction t JOIN billing.PaymentIntent i ON i.Id = t.PaymentIntentId, billing.BillingItem b
            WHERE t.TransactionReference = '{payment.TransactionReference}' AND b.Id = '{charge}'
            """));
        Assert.NotEqual(payment.PaymentIntentId, retry.PaymentIntentId);
        Assert.Equal(PaymentState.Pending, retry.Status);
    }

    [Fact]
    public async Task Failure_arriving_after_payment_does_not_overwrite_paid()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);
        await Payments(db).ConfirmVerifiedAsync(Verified(payment, "VNP-P1", "EVT-P1"), Ct);

        var late = await Payments(db).RecordFailedAsync(Failed(payment, "VNP-P1", "EVT-LATE"), Ct);

        Assert.Equal(PaymentFailureOutcome.IgnoredAlreadyPaid, late.Outcome);
        Assert.Equal(["Success|Succeeded"], await db.QueryAsync($"""
            SELECT t.PaymentStatus, i.Status FROM billing.PaymentTransaction t JOIN billing.PaymentIntent i ON i.Id = t.PaymentIntentId
            WHERE t.TransactionReference = '{payment.TransactionReference}'
            """));
    }

    [Fact]
    public async Task Success_for_a_transaction_reported_failed_is_a_conflict()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);
        await Payments(db).RecordFailedAsync(Failed(payment, "VNP-F2", "EVT-F2"), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).ConfirmVerifiedAsync(Verified(payment, "VNP-F2", "EVT-S2"), Ct));

        Assert.Equal(BillingErrorCode.PaymentConflict, error.Code);
    }

    [Fact]
    public async Task Paid_charge_is_not_payable_again()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var attempt = Guid.NewGuid();
        var payment = await Payments(db).CreateAsync(Exam(attempt, charge), Ct);
        await Payments(db).ConfirmVerifiedAsync(Verified(payment, "VNP-2", "EVT-2"), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).CreateAsync(Exam(attempt, charge), Ct));

        Assert.Equal(BillingErrorCode.ChargeNotPayable, error.Code);
    }

    [Fact]
    public async Task Unknown_transaction_reference_is_reported()
    {
        await using var db = await OpenAsync();

        var error = await Assert.ThrowsAsync<BillingException>(() => Payments(db).ConfirmVerifiedAsync(
            new VerifiedPayment("NO-SUCH-REF", "VNP-3", "EVT-3", 1m, "VND", DateTime.UtcNow, null), Ct));

        Assert.Equal(BillingErrorCode.PaymentNotFound, error.Code);
    }

    // ---- IPN có ký đi qua VnpayIpnHandler (đúng đường Billing.Api dùng) ----

    [Fact]
    public async Task Signed_success_ipn_is_recorded_and_resending_it_changes_nothing()
    {
        await using var db = await OpenAsync();
        var charge = await ExamChargeAsync(db);
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), charge), Ct);
        var ipn = Ipn(payment, "14226001");

        var first = await Handler(db).HandleAsync(ipn, Ct);
        var resent = await Handler(db).HandleAsync(ipn, Ct);

        Assert.Equal(PaymentCallbackOutcome.PaymentRecorded, first);
        Assert.Equal(PaymentCallbackOutcome.PaymentAlreadyRecorded, resent);
        Assert.Equal(["Paid|1|1|14226001"], await db.QueryAsync($"""
            SELECT b.Status,
                   (SELECT COUNT(*) FROM shared.OutboxMessage WHERE AggregateId = '{payment.PaymentIntentId}'),
                   (SELECT COUNT(*) FROM billing.PaymentEvent WHERE EventId = '14226001'),
                   (SELECT ProviderTransactionId FROM billing.PaymentTransaction WHERE TransactionReference = '{payment.TransactionReference}')
            FROM billing.BillingItem b WHERE b.Id = '{charge}'
            """));
    }

    [Fact]
    public async Task Cancelled_payments_carry_no_gateway_transaction_and_do_not_collide()
    {
        await using var db = await OpenAsync();
        var first = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);
        var second = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);

        Assert.Equal(PaymentCallbackOutcome.FailureRecorded, await Handler(db).HandleAsync(Ipn(first, "0", responseCode: "24", status: "02"), Ct));
        Assert.Equal(PaymentCallbackOutcome.FailureRecorded, await Handler(db).HandleAsync(Ipn(second, "0", responseCode: "24", status: "02"), Ct));
        Assert.Equal(PaymentCallbackOutcome.FailureAlreadyRecorded, await Handler(db).HandleAsync(Ipn(first, "0", responseCode: "24", status: "02"), Ct));

        Assert.Equal(["Failed|1"], await db.QueryAsync($"""
            SELECT MAX(PaymentStatus), COUNT(*) FROM billing.PaymentTransaction
            WHERE TransactionReference = '{first.TransactionReference}' AND ProviderTransactionId IS NULL
            """));
    }

    [Fact]
    public async Task Success_needs_both_codes()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);

        Assert.Equal(PaymentCallbackOutcome.FailureRecorded, await Handler(db).HandleAsync(Ipn(payment, "14226002", responseCode: "00", status: "02"), Ct));
    }

    [Fact]
    public async Task Result_that_is_not_final_records_nothing()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Handler(db).HandleAsync(Ipn(payment, "0", responseCode: "99", status: "01"), Ct));

        Assert.Equal(BillingErrorCode.InvalidRequest, error.Code);
        Assert.Equal(["Pending"], await TransactionStatusAsync(db, payment));
    }

    [Theory]
    [InlineData("tampered amount")]
    [InlineData("wrong secret")]
    [InlineData("no signature")]
    public async Task Ipn_without_a_valid_signature_records_nothing(string reason)
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);
        var ipn = reason == "wrong secret" ? Ipn(payment, "14226003", secret: "another-secret") : Ipn(payment, "14226003");
        if (reason == "tampered amount")
            ipn["vnp_Amount"] = "100";
        if (reason == "no signature")
            ipn.Remove(VnpaySignature.HashParameter);

        var error = await Assert.ThrowsAsync<BillingException>(() => Handler(db).HandleAsync(ipn, Ct));

        Assert.Equal(BillingErrorCode.InvalidSignature, error.Code);
        Assert.Equal(["Pending"], await TransactionStatusAsync(db, payment));
    }

    [Fact]
    public async Task Ipn_for_another_website_is_rejected()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Handler(db).HandleAsync(Ipn(payment, "14226004", tmnCode: "OTHERSIT"), Ct));

        Assert.Equal(BillingErrorCode.InvalidRequest, error.Code);
    }

    [Fact]
    public async Task Ipn_amount_is_divided_by_100_before_comparing()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Handler(db).HandleAsync(Ipn(payment, "14226005", amount: payment.Amount), Ct));

        Assert.Equal(BillingErrorCode.AmountMismatch, error.Code);
        Assert.Equal(["Pending"], await TransactionStatusAsync(db, payment));
    }

    [Fact]
    public async Task Amount_is_checked_before_an_ipn_is_reported_as_already_processed()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);
        await Handler(db).HandleAsync(Ipn(payment, "14226006"), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() => Handler(db).HandleAsync(Ipn(payment, "14226006", amount: 100m), Ct));

        Assert.Equal(BillingErrorCode.AmountMismatch, error.Code);
    }

    [Fact]
    public async Task Failure_ipn_with_another_amount_records_nothing()
    {
        await using var db = await OpenAsync();
        var payment = await Payments(db).CreateAsync(Exam(Guid.NewGuid(), await ExamChargeAsync(db)), Ct);

        var error = await Assert.ThrowsAsync<BillingException>(() =>
            Handler(db).HandleAsync(Ipn(payment, "0", responseCode: "24", status: "02", amount: 100m), Ct));

        Assert.Equal(BillingErrorCode.AmountMismatch, error.Code);
        Assert.Equal(["Pending"], await TransactionStatusAsync(db, payment));
    }

    private static Task<List<string>> TransactionStatusAsync(TestDatabase db, PaymentDto payment) =>
        db.QueryAsync($"SELECT PaymentStatus FROM billing.PaymentTransaction WHERE TransactionReference = '{payment.TransactionReference}'");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Mỗi lần gọi một cặp DbContext mới (Billing + Shared) trên cùng kết nối và transaction của test.
    private static PaymentService Payments(TestDatabase db)
    {
        var billing = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        return new PaymentService(new EfPaymentStore(billing, new OutboxWriter(shared, TimeProvider.System)), TimeProvider.System);
    }

    private static VnpayIpnHandler Handler(TestDatabase db) =>
        new(Payments(db), Options.Create(new VnpayOptions { TmnCode = TmnCode, HashSecret = Secret, ReturnUrl = "http://localhost/return" }), TimeProvider.System);

    private static VerifiedPayment Verified(PaymentDto payment, string providerTransactionId, string eventId) =>
        new(payment.TransactionReference, providerTransactionId, eventId, payment.Amount, "VND", DateTime.UtcNow, "{\"status\":\"Succeeded\"}");

    private static FailedPayment Failed(PaymentDto payment, string providerTransactionId, string eventId) =>
        new(payment.TransactionReference, providerTransactionId, eventId, payment.Amount, "VND", DateTime.UtcNow, "{\"status\":\"Failed\"}");

    // Amount là giá trị vnp_Amount gửi đi (đã nhân 100); mặc định đúng số tiền của giao dịch.
    private static Dictionary<string, string> Ipn(PaymentDto payment, string transactionNo, string responseCode = "00", string status = "00",
        decimal? amount = null, string tmnCode = TmnCode, string secret = Secret)
    {
        var parameters = new Dictionary<string, string>
        {
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_Amount"] = ((long)(amount ?? payment.Amount * 100m)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["vnp_BankCode"] = "NCB",
            ["vnp_OrderInfo"] = "Thanh toan phi kham ban dau",
            ["vnp_PayDate"] = "20261105090000",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TransactionNo"] = transactionNo,
            ["vnp_TransactionStatus"] = status,
            ["vnp_TxnRef"] = payment.TransactionReference,
        };
        parameters[VnpaySignature.HashParameter] = VnpaySignature.Sign(secret, VnpaySignature.Canonical(parameters));
        return parameters;
    }

    private static CreatePayment Exam(Guid attempt, Guid charge, PaymentProvider provider = PaymentProvider.VNPAY) =>
        new(PaymentPurpose.InitialConsultation, Guid.NewGuid(), Guid.NewGuid(), attempt, null, [charge], provider, DateTime.UtcNow.AddMinutes(5));

    private static CreatePayment Cls(Guid encounter, Guid[] charges) =>
        new(PaymentPurpose.CLSOrderBatch, Guid.NewGuid(), Guid.NewGuid(), null, encounter, charges, PaymentProvider.VNPAY, DateTime.UtcNow.AddMinutes(30));

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
