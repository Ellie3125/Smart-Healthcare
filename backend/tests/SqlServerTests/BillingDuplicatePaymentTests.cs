using Billing.Application;
using Billing.Contracts;
using Billing.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Trả trùng và tiền về cho khoản đã hủy (Phạm vi §5.6): ghi nhận tiền và tự tạo yêu cầu hoàn chờ duyệt. Dữ liệu giả lập, rollback sau mỗi test.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingDuplicatePaymentTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();

    [Fact]
    public async Task Vnpay_paid_after_cash_at_counter_is_refunded_in_full_and_not_announced_again()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        var charge = await ExamChargeAsync(db, 150_000m);
        var online = await Payments(db).CreateAsync(Exam(attempt, charge), Ct);
        await Services(db).Cashier.CollectExamFeeCashAsync(new CollectExamFeeCash(attempt, Patient, charge, Staff), Ct);

        // Bệnh nhân vẫn trả xong trên trang VNPAY đã mở trước khi tiếp tân thu.
        var late = await VerifyAsync(db, online);

        Assert.Equal((1, 0), (late.RefundsRequested, late.PaidChargeIds.Count));
        Assert.Equal(["Pending|DuplicatePayment|CustomApproval|150000.00".ToUpperInvariant()], await RefundsAsync(db, charge));
        Assert.Equal(["Paid|300000.00"], await ChargeAsync(db, charge));
        Assert.Empty(await db.QueryAsync($"SELECT Id FROM shared.OutboxMessage WHERE AggregateId = '{online.PaymentIntentId}'"));
    }

    [Fact]
    public async Task Late_ipn_of_an_expired_cls_request_refunds_the_overpaid_part()
    {
        await using var db = await OpenAsync();
        var (encounter, charge) = await ClsChargeAsync(db, 200_000m);
        var first = await Payments(db).CreateAsync(Cls(encounter, charge), Ct);
        await ExpireAsync(db, first);
        var second = await Payments(db).CreateAsync(Cls(encounter, charge), Ct);

        var paid = await VerifyAsync(db, second);
        var late = await VerifyAsync(db, first);

        Assert.Equal((0, 1), (paid.RefundsRequested, late.RefundsRequested));
        Assert.Equal(["Pending|DuplicatePayment|CustomApproval|200000.00".ToUpperInvariant()], await RefundsAsync(db, charge));
        Assert.Equal(["Paid|400000.00"], await ChargeAsync(db, charge));
        Assert.Equal([first.TransactionReference], await db.QueryAsync($"""
            SELECT t.TransactionReference FROM billing.RefundRequest r JOIN billing.PaymentTransaction t ON t.Id = r.OriginalPaymentTransactionId
            WHERE r.BillingItemId = '{charge}'
            """));
    }

    [Fact]
    public async Task Second_exam_payment_of_the_same_booking_is_refunded_and_its_fee_cancelled()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        // Giữ chỗ lại: mỗi lần giữ một khoản phí khám và một yêu cầu thanh toán; cả hai lần đều được trả.
        var firstCharge = await ExamChargeAsync(db, 150_000m);
        var first = await Payments(db).CreateAsync(Exam(attempt, firstCharge), Ct);
        await ExpireAsync(db, first);
        var secondCharge = await ExamChargeAsync(db, 150_000m);
        var second = await Payments(db).CreateAsync(Exam(attempt, secondCharge), Ct);

        var kept = await VerifyAsync(db, second);
        var duplicate = await VerifyAsync(db, first);

        Assert.Equal([secondCharge], kept.PaidChargeIds);
        Assert.Equal(0, kept.RefundsRequested);
        Assert.Equal((1, 0), (duplicate.RefundsRequested, duplicate.PaidChargeIds.Count));
        Assert.Equal(["Pending|DuplicatePayment|CustomApproval|150000.00".ToUpperInvariant()], await RefundsAsync(db, firstCharge));
        Assert.Equal(["Cancelled|150000.00"], await ChargeAsync(db, firstCharge));
        Assert.Equal(["Paid|150000.00"], await ChargeAsync(db, secondCharge));
        Assert.Empty(await RefundsAsync(db, secondCharge));
        Assert.Empty(await db.QueryAsync($"SELECT Id FROM shared.OutboxMessage WHERE AggregateId = '{first.PaymentIntentId}'"));
    }

    [Fact]
    public async Task Money_for_a_cancelled_charge_is_refunded_as_paid_unallocated()
    {
        await using var db = await OpenAsync();
        var (encounter, charge) = await ClsChargeAsync(db, 200_000m);
        var payment = await Payments(db).CreateAsync(Cls(encounter, charge), Ct);
        // Job cuối ngày D hủy khoản chưa trả, rồi IPN của lần trả trước đó mới về.
        await db.QueryAsync($"UPDATE billing.BillingItem SET Status = 'Cancelled' WHERE Id = '{charge}'; SELECT 1;");

        var late = await VerifyAsync(db, payment);

        Assert.Equal((1, 0), (late.RefundsRequested, late.PaidChargeIds.Count));
        Assert.Equal(["Pending|PaidUnallocated|CustomApproval|200000.00".ToUpperInvariant()], await RefundsAsync(db, charge));
        Assert.Equal(["Cancelled|200000.00"], await ChargeAsync(db, charge));
    }

    [Fact]
    public async Task Single_payment_and_replayed_ipn_create_no_refund()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        var charge = await ExamChargeAsync(db, 150_000m);
        var payment = await Payments(db).CreateAsync(Exam(attempt, charge), Ct);
        var verified = Verified(payment);

        var first = await Payments(db).ConfirmVerifiedAsync(verified, Ct);
        var replay = await Payments(db).ConfirmVerifiedAsync(verified, Ct);

        Assert.Equal((0, false, true), (first.RefundsRequested, first.AlreadyRecorded, replay.AlreadyRecorded));
        Assert.Empty(await RefundsAsync(db, charge));
        Assert.Equal(["Paid|150000.00"], await ChargeAsync(db, charge));
    }

    [Fact]
    public async Task Payment_after_an_earlier_one_was_sent_for_refund_is_kept()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        // Lần trả đầu đến muộn, không giữ được suất: Scheduling yêu cầu hoàn (PaidUnallocated). Bệnh nhân giữ chỗ lại và trả lần nữa.
        var firstCharge = await ExamChargeAsync(db, 150_000m);
        var first = await Payments(db).CreateAsync(Exam(attempt, firstCharge), Ct);
        await VerifyAsync(db, first);
        await Services(db).Refunds.RequestPaidUnallocatedRefundAsync(first.PaymentIntentId, Ct);
        var secondCharge = await ExamChargeAsync(db, 150_000m);
        var second = await Payments(db).CreateAsync(Exam(attempt, secondCharge), Ct);

        var kept = await VerifyAsync(db, second);

        Assert.Equal([secondCharge], kept.PaidChargeIds);
        Assert.Equal(0, kept.RefundsRequested);
        Assert.Empty(await RefundsAsync(db, secondCharge));
        Assert.Equal(["PaymentVerified"], await db.QueryAsync($"SELECT EventType FROM shared.OutboxMessage WHERE AggregateId = '{second.PaymentIntentId}'"));
    }

    [Fact]
    public async Task Late_payment_after_the_appointment_was_cancelled_and_refunded_is_refunded_when_scheduling_reports_it()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        var firstCharge = await ExamChargeAsync(db, 150_000m);
        var first = await Payments(db).CreateAsync(Exam(attempt, firstCharge), Ct);
        await ExpireAsync(db, first);
        var secondCharge = await ExamChargeAsync(db, 150_000m);
        var second = await Payments(db).CreateAsync(Exam(attempt, secondCharge), Ct);
        await VerifyAsync(db, second);
        await Services(db).Refunds.RequestForCancellationAsync(new Scheduling.Contracts.AppointmentCancelled(Guid.NewGuid(), Guid.NewGuid(), attempt,
            Patient, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), Scheduling.Contracts.CancellationInitiator.Patient,
            Scheduling.Contracts.ConsultationRefund.Full, true, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow, null), Ct);

        // Lần trả cũ về sau khi lịch đã hủy và đã hoàn: Billing chưa thấy trùng (tiền lần trước đang chờ hoàn) nên báo Scheduling;
        // Scheduling gặp AlreadyConfirmed và yêu cầu hoàn.
        var late = await VerifyAsync(db, first);
        var requested = await Services(db).Refunds.RequestDuplicatePaymentRefundAsync(first.PaymentIntentId, Ct);
        var again = await Services(db).Refunds.RequestDuplicatePaymentRefundAsync(first.PaymentIntentId, Ct);

        Assert.Equal([firstCharge], late.PaidChargeIds);
        Assert.Equal((1, 0), (requested, again));
        Assert.Equal(["Pending|DuplicatePayment|CustomApproval|150000.00".ToUpperInvariant()], await RefundsAsync(db, firstCharge));
        Assert.Equal(["Cancelled|150000.00"], await ChargeAsync(db, firstCharge));
        Assert.Equal(["Pending|CancelledBefore0500|FullRefundPre0500|150000.00".ToUpperInvariant()], await RefundsAsync(db, secondCharge));
    }

    [Fact]
    public async Task Counter_refuses_exam_cash_when_the_booking_is_already_paid_online()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        var paidCharge = await ExamChargeAsync(db, 150_000m);
        await VerifyAsync(db, await Payments(db).CreateAsync(Exam(attempt, paidCharge), Ct));
        var newCharge = await ExamChargeAsync(db, 150_000m);

        var error = await Assert.ThrowsAsync<BillingException>(() =>
            Services(db).Cashier.CollectExamFeeCashAsync(new CollectExamFeeCash(attempt, Patient, newCharge, Staff), Ct));

        Assert.Equal(BillingErrorCode.ChargeNotPayable, error.Code);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<List<string>> RefundsAsync(TestDatabase db, Guid charge) => db.QueryAsync($"""
        SELECT UPPER(CONCAT(Status, '|', RefundReason, '|', PolicyRuleApplied, '|', CONVERT(varchar(20), RequestedAmount)))
        FROM billing.RefundRequest WHERE BillingItemId = '{charge}'
        """);

    private static Task<List<string>> ChargeAsync(TestDatabase db, Guid charge) =>
        db.QueryAsync($"SELECT CONCAT(Status, '|', CONVERT(varchar(20), PaidAmount)) FROM billing.BillingItem WHERE Id = '{charge}'");

    private static Task ExpireAsync(TestDatabase db, PaymentDto payment) => db.QueryAsync($"""
        UPDATE billing.PaymentIntent SET ExpiresAtUtc = DATEADD(minute, -1, SYSUTCDATETIME()) WHERE Id = '{payment.PaymentIntentId}';
        SELECT 1;
        """);

    private static VerifiedPayment Verified(PaymentDto payment)
    {
        var txn = "VNP-" + Guid.NewGuid().ToString("N")[..12];
        return new VerifiedPayment(payment.TransactionReference, txn, txn, payment.Amount, "VND", DateTime.UtcNow, "{}");
    }

    private static Task<PaymentConfirmation> VerifyAsync(TestDatabase db, PaymentDto payment) =>
        Payments(db).ConfirmVerifiedAsync(Verified(payment), Ct);

    private static CreatePayment Exam(Guid attempt, Guid charge) =>
        new(PaymentPurpose.InitialConsultation, Patient, Payer, attempt, null, [charge], PaymentProvider.VNPAY, DateTime.UtcNow.AddMinutes(5));

    private static CreatePayment Cls(Guid encounter, Guid charge) =>
        new(PaymentPurpose.CLSOrderBatch, Patient, Payer, null, encounter, [charge], PaymentProvider.VNPAY, DateTime.UtcNow.AddMinutes(15));

    private static async Task<Guid> PriceItemAsync(TestDatabase db, string type, decimal price)
    {
        var list = await db.InsertAsync("billing.PriceList", new
        {
            PriceListCode = "T-" + Guid.NewGuid().ToString("N")[..10], Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
        });
        return type == "ConsultationFee"
            ? await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = type, DoctorRankId = Guid.NewGuid(), UnitPrice = price, IsActive = true })
            : await db.InsertAsync("billing.PriceListItem", new { PriceListId = list, ItemType = type, MedicalServiceId = Guid.NewGuid(), UnitPrice = price, IsActive = true });
    }

    private static async Task<Guid> ExamChargeAsync(TestDatabase db, decimal price) =>
        await db.InsertAsync("billing.BillingItem", new
        {
            PriceListItemId = await PriceItemAsync(db, "ConsultationFee", price), ItemType = "ConsultationFee", ItemNameSnapshot = "Phí khám giả lập",
            UnitPrice = price, Quantity = 1, TotalAmount = price, Status = "Pending", CreatedAtUtc = DateTime.UtcNow,
        });

    private static async Task<(Guid Encounter, Guid Charge)> ClsChargeAsync(TestDatabase db, decimal price)
    {
        var encounter = Guid.NewGuid();
        var charge = await db.InsertAsync("billing.BillingItem", new
        {
            EncounterId = encounter, ServiceOrderItemId = Guid.NewGuid(), ServiceOrderId = Guid.NewGuid(), PatientId = Patient,
            PriceListItemId = await PriceItemAsync(db, "CLSService", price), ItemType = "CLSService", ItemNameSnapshot = "Dịch vụ CLS giả lập",
            UnitPrice = price, Quantity = 1, TotalAmount = price, Status = "Pending", CreatedAtUtc = DateTime.UtcNow,
        });
        return (encounter, charge);
    }

    private static PaymentService Payments(TestDatabase db) => new(Services(db).Store, TimeProvider.System);

    // Billing, Outbox và AuditLog dùng chung một SharedDbContext như trong DI.
    private static (EfPaymentStore Store, CashierService Cashier, RefundService Refunds) Services(TestDatabase db)
    {
        var billing = db.CreateContext<BillingDbContext>(options => new BillingDbContext(options));
        var shared = db.CreateContext<SharedDbContext>(options => new SharedDbContext(options));
        var store = new EfPaymentStore(billing, new OutboxWriter(shared, TimeProvider.System));
        var charges = new EfChargeStore(billing);
        return (store,
            new CashierService(charges, store, new EfCashierStore(billing, shared, TimeProvider.System), TimeProvider.System),
            new RefundService(store, new EfRefundStore(billing, shared, TimeProvider.System), charges, TimeProvider.System));
    }
}
