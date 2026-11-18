using Billing.Application;
using Billing.Contracts;
using Billing.Domain.Enums;
using Billing.Infrastructure.Persistence;
using Scheduling.Contracts;
using Shared.Infrastructure.Persistence;
using Xunit;
using static SqlServerTests.TestDatabase;

namespace SqlServerTests;

// Thu tiền mặt phí khám tại quầy và hoàn tiền mặt (hủy lịch, PaidUnallocated) trên SQL Server thật. Dữ liệu giả lập, rollback sau mỗi test.
[Collection(nameof(BillingPaymentSerialCollection))]
public class BillingRefundTests
{
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Payer = Guid.NewGuid();
    private static readonly Guid Patient = Guid.NewGuid();

    [Fact]
    public async Task Exam_fee_cash_collection_writes_one_counter_receipt_and_cancels_the_online_request()
    {
        await using var db = await OpenAsync();
        var attempt = Guid.NewGuid();
        var charge = await ExamChargeAsync(db, 150_000m);
        var online = await Payments(db).CreateAsync(Exam(attempt, charge), Ct);

        var first = await Services(db).Cashier.CollectExamFeeCashAsync(new CollectExamFeeCash(attempt, Patient, charge, Staff, "10.0.0.7"), Ct);
        var again = await Services(db).Cashier.CollectExamFeeCashAsync(new CollectExamFeeCash(attempt, Patient, charge, Staff), Ct);

        Assert.Equal((150_000m, false), (first.Amount, first.AlreadyCollected));
        Assert.StartsWith("PT", first.ReceiptNumber);
        Assert.Equal((first.ReceiptNumber, true), (again.ReceiptNumber, again.AlreadyCollected));
        Assert.Equal(["Paid"], await db.QueryAsync($"SELECT Status FROM billing.BillingItem WHERE Id = '{charge}'"));
        Assert.Equal(["Cancelled"], await db.QueryAsync($"SELECT Status FROM billing.PaymentIntent WHERE Id = '{online.PaymentIntentId}'"));
        Assert.Equal([$"Counter|Succeeded|InitialConsultation|{Staff}".ToUpperInvariant()], await db.QueryAsync($"""
            SELECT UPPER(CONCAT(Provider, '|', Status, '|', PaymentType, '|', PayerAppUserId)) FROM billing.PaymentIntent
            WHERE BookingAttemptId = '{attempt}' AND Provider = 'Counter'
            """));
        // Scheduling xác nhận lịch bằng đúng PaymentVerified của luồng VNPAY.
        Assert.Equal(["PaymentVerified"], await db.QueryAsync($"SELECT EventType FROM shared.OutboxMessage WHERE AggregateId = '{first.PaymentIntentId}'"));
        Assert.Equal([$"{Staff}|10.0.0.7".ToUpperInvariant()], await db.QueryAsync($"""
            SELECT UPPER(CONCAT(a.PerformedByUserId, '|', a.ClientIpAddress))
            FROM shared.AuditLog a JOIN billing.PaymentTransaction t ON t.Id = a.RecordId
            WHERE t.TransactionReference = '{first.ReceiptNumber}'
            """));
    }

    [Fact]
    public async Task Exam_fee_already_paid_online_is_not_collected_twice()
    {
        await using var db = await OpenAsync();
        var (attempt, charge, _) = await PaidExamFeeAsync(db, 150_000m);

        var error = await Assert.ThrowsAsync<BillingException>(() =>
            Services(db).Cashier.CollectExamFeeCashAsync(new CollectExamFeeCash(attempt, Patient, charge, Staff), Ct));

        Assert.Equal(BillingErrorCode.ChargeNotPayable, error.Code);
    }

    [Fact]
    public async Task Cancellation_with_full_refund_creates_one_pending_request_and_cancels_the_fee()
    {
        await using var db = await OpenAsync();
        var (attempt, charge, _) = await PaidExamFeeAsync(db, 150_000m);
        var cancelled = Cancelled(attempt, CancellationInitiator.Patient, ConsultationRefund.Full);

        var first = await Services(db).Refunds.RequestForCancellationAsync(cancelled, Ct);
        var replay = await Services(db).Refunds.RequestForCancellationAsync(cancelled, Ct); // Outbox giao lặp

        Assert.Equal((1, 0), (first, replay));
        Assert.Equal([$"Pending|CancelledBefore0500|FullRefundPre0500|150000.00|{cancelled.AppointmentId}".ToUpperInvariant()],
            await db.QueryAsync($"""
                SELECT UPPER(CONCAT(Status, '|', RefundReason, '|', PolicyRuleApplied, '|', CONVERT(varchar(20), RequestedAmount), '|', AppointmentId))
                FROM billing.RefundRequest WHERE BillingItemId = '{charge}'
                """));
        Assert.Equal(["Cancelled"], await db.QueryAsync($"SELECT Status FROM billing.BillingItem WHERE Id = '{charge}'"));
    }

    [Fact]
    public async Task Hospital_cancellation_needs_custom_approval_and_late_patient_cancellation_refunds_nothing()
    {
        await using var db = await OpenAsync();
        var (hospitalAttempt, hospitalCharge, _) = await PaidExamFeeAsync(db, 300_000m);
        var (lateAttempt, lateCharge, _) = await PaidExamFeeAsync(db, 150_000m);

        await Services(db).Refunds.RequestForCancellationAsync(Cancelled(hospitalAttempt, CancellationInitiator.Hospital, ConsultationRefund.Full), Ct);
        var late = await Services(db).Refunds.RequestForCancellationAsync(Cancelled(lateAttempt, CancellationInitiator.Patient, ConsultationRefund.None), Ct);

        Assert.Equal(["HospitalCancelled|CustomApproval".ToUpperInvariant()], await db.QueryAsync(
            $"SELECT UPPER(CONCAT(RefundReason, '|', PolicyRuleApplied)) FROM billing.RefundRequest WHERE BillingItemId = '{hospitalCharge}'"));
        Assert.Equal(0, late);
        Assert.Empty(await db.QueryAsync($"SELECT Id FROM billing.RefundRequest WHERE BillingItemId = '{lateCharge}'"));
        Assert.Equal(["Paid"], await db.QueryAsync($"SELECT Status FROM billing.BillingItem WHERE Id = '{lateCharge}'"));
    }

    [Fact]
    public async Task Paid_unallocated_payment_gets_one_refund_request()
    {
        await using var db = await OpenAsync();
        var (_, charge, intent) = await PaidExamFeeAsync(db, 150_000m);

        var first = await Services(db).Refunds.RequestPaidUnallocatedRefundAsync(intent, Ct);
        var again = await Services(db).Refunds.RequestPaidUnallocatedRefundAsync(intent, Ct);
        var unknown = await Services(db).Refunds.RequestPaidUnallocatedRefundAsync(Guid.NewGuid(), Ct);

        Assert.Equal((1, 0, 0), (first, again, unknown));
        Assert.Equal(["Pending|PaidUnallocated|CustomApproval".ToUpperInvariant()], await db.QueryAsync(
            $"SELECT UPPER(CONCAT(Status, '|', RefundReason, '|', PolicyRuleApplied)) FROM billing.RefundRequest WHERE BillingItemId = '{charge}'"));
        Assert.Equal(["Cancelled"], await db.QueryAsync($"SELECT Status FROM billing.BillingItem WHERE Id = '{charge}'"));
    }

    [Fact]
    public async Task Approved_refund_is_disbursed_in_cash_once_and_shift_summary_nets_it_out()
    {
        await using var db = await OpenAsync();
        // Bệnh nhân đăng ký tại quầy, trả tiền mặt rồi hủy trước 05:00.
        var attempt = Guid.NewGuid();
        var charge = await ExamChargeAsync(db, 150_000m);
        await Services(db).Cashier.CollectExamFeeCashAsync(new CollectExamFeeCash(attempt, Patient, charge, Staff), Ct);
        await Services(db).Refunds.RequestForCancellationAsync(Cancelled(attempt, CancellationInitiator.Receptionist, ConsultationRefund.Full), Ct);
        var request = Assert.Single(await Services(db).Refunds.ListAsync(new RefundQuery(PatientId: Patient), Ct));

        var tooEarly = await Services(db).Refunds.DisburseCashAsync(request.RefundRequestId, Staff, null, Ct);
        var approved = await Services(db).Refunds.DecideAsync(request.RefundRequestId, Admin, approve: true, "Hủy trước 05:00", "10.0.0.9", Ct);
        var decidedAgain = await Services(db).Refunds.DecideAsync(request.RefundRequestId, Admin, approve: false, null, null, Ct);
        var paid = await Services(db).Refunds.DisburseCashAsync(request.RefundRequestId, Staff, "10.0.0.7", Ct);
        var paidAgain = await Services(db).Refunds.DisburseCashAsync(request.RefundRequestId, Staff, null, Ct);

        Assert.Equal((RefundChangeOutcome.InvalidState, "Pending"), (tooEarly.Change.Outcome, tooEarly.Change.CurrentStatus));
        Assert.Equal(("Approved", Admin), (approved.Refund!.Status, approved.Refund.DecidedByUserId));
        Assert.Equal((RefundChangeOutcome.InvalidState, "Approved"), (decidedAgain.Outcome, decidedAgain.CurrentStatus));
        var slip = paid.Slip!;
        Assert.StartsWith("PH", slip.RefundSlipNumber);
        Assert.Equal((150_000m, Patient), (slip.Amount, slip.PatientId));
        Assert.Equal(("Settled", slip.RefundSlipNumber), (paid.Change.Refund!.Status, paid.Change.Refund.RefundSlipNumber));
        Assert.Equal((RefundChangeOutcome.InvalidState, "Settled", (CashRefundSlip?)null),
            (paidAgain.Change.Outcome, paidAgain.Change.CurrentStatus, paidAgain.Slip));
        Assert.Equal(["1"], await db.QueryAsync($"SELECT COUNT(*) FROM billing.RefundTransaction WHERE RefundRequestId = '{request.RefundRequestId}'"));
        Assert.Equal(["Cancelled|150000.00|150000.00".ToUpperInvariant()], await db.QueryAsync($"""
            SELECT UPPER(CONCAT(Status, '|', CONVERT(varchar(20), PaidAmount), '|', CONVERT(varchar(20), RefundedAmount)))
            FROM billing.BillingItem WHERE Id = '{charge}'
            """));
        Assert.Equal(["billing.RefundRequest", "billing.RefundTransaction"], await db.QueryAsync($"""
            SELECT TableName FROM shared.AuditLog
            WHERE RecordId = '{request.RefundRequestId}'
               OR RecordId IN (SELECT Id FROM billing.RefundTransaction WHERE RefundRequestId = '{request.RefundRequestId}')
            ORDER BY TableName
            """));

        var summary = await Services(db).Cashier.GetTodaySummaryAsync(Staff, Ct);
        Assert.Equal((150_000m, 150_000m, 0m), (summary.CashCollected, summary.CashRefunded, summary.NetCash));
        Assert.Equal(slip.RefundSlipNumber, Assert.Single(summary.Refunds).RefundSlipNumber);
    }

    [Fact]
    public async Task Rejected_refund_cannot_be_disbursed_and_lists_filter_by_status_and_patient()
    {
        await using var db = await OpenAsync();
        var (attempt, _, _) = await PaidExamFeeAsync(db, 150_000m);
        await Services(db).Refunds.RequestForCancellationAsync(Cancelled(attempt, CancellationInitiator.Patient, ConsultationRefund.Full), Ct);
        var request = Assert.Single(await Services(db).Refunds.ListAsync(new RefundQuery(RefundRequestStatus.Pending, Patient), Ct));

        var rejected = await Services(db).Refunds.DecideAsync(request.RefundRequestId, Admin, approve: false, "Đã đổi sang lịch khác", null, Ct);
        var disbursed = await Services(db).Refunds.DisburseCashAsync(request.RefundRequestId, Staff, null, Ct);

        Assert.Equal("Rejected", rejected.Refund!.Status);
        Assert.Equal((RefundChangeOutcome.InvalidState, "Rejected"), (disbursed.Change.Outcome, disbursed.Change.CurrentStatus));
        Assert.Empty(await Services(db).Refunds.ListAsync(new RefundQuery(RefundRequestStatus.Pending, Patient), Ct));
        Assert.Empty(await Services(db).Refunds.ListAsync(new RefundQuery(PatientId: Guid.NewGuid()), Ct));
        Assert.Equal(RefundChangeOutcome.NotFound, (await Services(db).Refunds.DecideAsync(Guid.NewGuid(), Admin, true, null, null, Ct)).Outcome);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AppointmentCancelled Cancelled(Guid attempt, CancellationInitiator by, ConsultationRefund refund) =>
        new(Guid.NewGuid(), Guid.NewGuid(), attempt, Patient, Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), by, refund,
            SlotReleased: refund == ConsultationRefund.Full, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), DateTime.UtcNow, null);

    private static CreatePayment Exam(Guid attempt, Guid charge) =>
        new(PaymentPurpose.InitialConsultation, Patient, Payer, attempt, null, [charge], PaymentProvider.VNPAY, DateTime.UtcNow.AddMinutes(5));

    // Phí khám đã thu qua VNPAY: tạo khoản, yêu cầu thanh toán, rồi ghi nhận IPN đã xác minh.
    private static async Task<(Guid Attempt, Guid Charge, Guid PaymentIntent)> PaidExamFeeAsync(TestDatabase db, decimal price)
    {
        var attempt = Guid.NewGuid();
        var charge = await ExamChargeAsync(db, price);
        var payment = await Payments(db).CreateAsync(Exam(attempt, charge), Ct);
        var txn = "VNP-" + Guid.NewGuid().ToString("N")[..12];
        await Payments(db).ConfirmVerifiedAsync(new VerifiedPayment(payment.TransactionReference, txn, txn, payment.Amount, "VND", DateTime.UtcNow, "{}"), Ct);
        return (attempt, charge, payment.PaymentIntentId);
    }

    private static async Task<Guid> ExamChargeAsync(TestDatabase db, decimal price)
    {
        var list = await db.InsertAsync("billing.PriceList", new
        {
            PriceListCode = "T-" + Guid.NewGuid().ToString("N")[..10], Currency = "VND", IsActive = true, EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
        });
        var item = await db.InsertAsync("billing.PriceListItem", new
        {
            PriceListId = list, ItemType = "ConsultationFee", DoctorRankId = Guid.NewGuid(), UnitPrice = price, IsActive = true,
        });
        return await db.InsertAsync("billing.BillingItem", new
        {
            PriceListItemId = item, ItemType = "ConsultationFee", ItemNameSnapshot = "Phí khám giả lập",
            UnitPrice = price, Quantity = 1, TotalAmount = price, Status = "Pending", CreatedAtUtc = DateTime.UtcNow,
        });
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
