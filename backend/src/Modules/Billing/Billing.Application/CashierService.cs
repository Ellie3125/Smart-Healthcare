using System.Text.Json;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Shared.Abstractions;

namespace Billing.Application;

/// <summary>Một đợt CLS trên màn hình quầy thu.</summary>
public sealed record CashierClsItem(
    Guid ServiceOrderId,
    Guid PatientId,
    Guid EncounterId,
    string Description,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal Outstanding,
    ClsBatchStatus Status,
    DateTime OrderedAtUtc,
    DateTime PayByUtc);

public enum CashCollectionOutcome
{
    Collected,
    NotFound,
    NothingToPay,
    PastDeadline,
}

/// <param name="ReceiptNumber">Số phiếu thu (PaymentTransaction.TransactionReference).</param>
/// <param name="CancelledOnlineRequests">Số yêu cầu thanh toán VNPAY đang mở của các khoản này đã bị hủy vì đã thu tiền mặt.</param>
public sealed record CashReceipt(
    string ReceiptNumber,
    Guid PaymentIntentId,
    Guid ServiceOrderId,
    Guid PatientId,
    Guid EncounterId,
    decimal Amount,
    string Currency,
    DateTime CollectedAtUtc,
    int CancelledOnlineRequests);

public sealed record CashCollection(CashCollectionOutcome Outcome, CashReceipt? Receipt);

/// <param name="ShiftDate">Ngày theo giờ Việt Nam.</param>
/// <param name="CashRefunded">Tiền mặt tiếp tân đã chi hoàn tại quầy trong ngày.</param>
/// <param name="NetCash">Tiền mặt phải có trong két: đã thu − đã hoàn.</param>
public sealed record CashierShiftSummary(
    DateOnly ShiftDate,
    Guid CashierUserId,
    int ReceiptCount,
    decimal CashCollected,
    decimal CashRefunded,
    decimal NetCash,
    string Currency,
    IReadOnlyList<CounterReceipt> Receipts,
    IReadOnlyList<CashRefundSlip> Refunds,
    bool IsClosed = false, DateTime? ClosedAt = null, decimal? ActualCashCounted = null, string? DiscrepancyNote = null,
    string CashierName = "")
{
    public decimal ExpectedCashCollected => CashCollected;
    public decimal ExpectedCashRefunded => CashRefunded;
    public decimal NetExpectedCash => NetCash;
}

/// <summary>
/// Thu tiền mặt tại quầy (Giải pháp kỹ thuật v2.13 mục 5.5): phí CLS theo đợt chỉ định và phí khám của lượt đăng ký tại quầy
/// (<see cref="ICounterExamFeeCollector"/>). Tiếp tân xác nhận đã nhận tiền thì ghi nhận đã xác minh ngay: PaymentIntent
/// Provider = Counter, PaymentTransaction Success với số phiếu thu làm TransactionReference, phân bổ vào từng khoản,
/// PaymentVerified vào Outbox và AuditLog trong cùng một transaction.
/// Với Provider = Counter, PaymentIntent.PayerAppUserId là tài khoản tiếp tân đã nhận tiền; đối soát ca dựa trên trường này.
/// </summary>
public sealed class CashierService(IChargeStore charges, IPaymentStore store, ICashierStore cashier, TimeProvider clock, IDbClock? dbClock = null)
    : ICounterExamFeeCollector
{
    public const int ListLimit = 500;

    /// <summary>Đợt CLS trong ngày (giờ Việt Nam), đợt còn phải thu trước, rồi mới nhất trước.</summary>
    public async Task<IReadOnlyList<CashierClsItem>> ListTodayAsync(Guid? patientId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var from = ClsBatches.StartOfDayUtc(now);
        var items = patientId is { } id
            ? await charges.GetClsByPatientAsync(id, from, ct)
            : await charges.GetClsSinceAsync(from, ListLimit, ct);

        return items
            .Where(i => i.ServiceOrderId is not null && i.EncounterId is not null && i.PatientId is not null)
            .GroupBy(i => i.ServiceOrderId!.Value)
            .Select(g => ClsBatches.Build(g.ToList(), now))
            .OrderBy(b => b.Status == ClsBatchStatus.PendingPayment ? 0 : 1)
            .ThenByDescending(b => b.OrderedAtUtc)
            .Select(b => new CashierClsItem(b.ServiceOrderId, b.PatientId, b.EncounterId,
                string.Join(", ", b.Items.Where(i => i.Status is not (ChargeStatus.Cancelled or ChargeStatus.Waived)).Select(i => i.ServiceName)),
                b.TotalAmount, b.PaidAmount, b.Outstanding, b.Status, b.OrderedAtUtc, b.PayByUtc))
            .ToList();
    }

    /// <summary>
    /// Thu tiền mặt phần còn phải trả của đợt CLS. Yêu cầu VNPAY đang mở của các khoản này chuyển Cancelled; nếu bệnh nhân vẫn
    /// trả xong trên VNPAY, IPN đến sau vẫn được ghi nhận và phần thừa thành tiền chờ hoàn (DuplicatePayment).
    /// </summary>
    /// <exception cref="BillingException">ChargeNotPayable khi khoản vừa được trả hoặc hủy giữa lúc xem và lúc thu.</exception>
    public async Task<CashCollection> CollectClsAsync(Guid serviceOrderId, Guid staffUserId, string? clientIp, CancellationToken ct)
    {
        if (staffUserId == Guid.Empty)
            throw new BillingException(BillingErrorCode.InvalidRequest, "Thiếu tài khoản tiếp tân.");

        return await store.InTransactionAsync(async token =>
        {
            var now = await OpenShiftTimeAsync(staffUserId, token);
            return await CollectClsCoreAsync(serviceOrderId, staffUserId, clientIp, now, token);
        }, ct);
    }

    private async Task<CashCollection> CollectClsCoreAsync(Guid serviceOrderId, Guid staffUserId, string? clientIp, DateTime now, CancellationToken ct)
    {

        var items = await charges.GetClsByServiceOrderAsync(serviceOrderId, ct);
        if (items.Count == 0 || items.Any(i => i.PatientId is null || i.EncounterId is null)
            || items.Select(i => i.PatientId).Distinct().Count() != 1)
            return new CashCollection(CashCollectionOutcome.NotFound, null);

        var batch = ClsBatches.Build(items, now);
        switch (batch.Status)
        {
            case ClsBatchStatus.Paid or ClsBatchStatus.Cancelled:
                return new CashCollection(CashCollectionOutcome.NothingToPay, null);
            case ClsBatchStatus.Expired:
                return new CashCollection(CashCollectionOutcome.PastDeadline, null);
        }

        var ids = items.Where(i => i.Outstanding > 0m).Select(i => i.Id).Order().ToList();
        var receipt = await store.InTransactionAsync(async token =>
        {
            var locked = await store.LockChargesAsync(ids, token);
            if (locked.Count != ids.Count || locked.Any(c => c.Outstanding <= 0m))
                throw new BillingException(BillingErrorCode.ChargeNotPayable,
                    $"Đợt chỉ định {serviceOrderId} vừa thay đổi (đã trả hoặc đã hủy); tải lại trước khi thu.");

            var cash = await RecordCashAsync(locked, PaymentPurpose.CLSOrderBatch, batch.PatientId, bookingAttemptId: null, batch.EncounterId,
                staffUserId, now, new { serviceOrderId }, clientIp, token);
            return new CashReceipt(cash.ReceiptNumber, cash.PaymentIntentId, serviceOrderId, batch.PatientId, batch.EncounterId, cash.Amount,
                BillingCurrency.Vnd, now, cash.CancelledOnlineRequests);
        }, ct);

        return new CashCollection(CashCollectionOutcome.Collected, receipt);
    }

    public async Task<ExamFeeCashReceipt> CollectExamFeeCashAsync(CollectExamFeeCash command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.BookingAttemptId == Guid.Empty || command.PatientId == Guid.Empty || command.ChargeId == Guid.Empty
            || command.ReceptionistAppUserId == Guid.Empty)
            throw new BillingException(BillingErrorCode.InvalidRequest, "Thiếu lượt đặt lịch, hồ sơ, khoản phí khám hoặc tài khoản tiếp tân.");

        return await store.InTransactionAsync(async token =>
        {
            var now = await OpenShiftTimeAsync(command.ReceptionistAppUserId, token);
            // Cùng thứ tự khóa với PaymentService.CreateAsync: lượt đặt lịch rồi khoản thu.
            await store.LockBookingAttemptAsync(command.BookingAttemptId, token);
            var charge = (await store.LockChargesAsync([command.ChargeId], token)).SingleOrDefault();
            if (charge is null || charge.ItemType != ItemType.ConsultationFee)
                throw new BillingException(BillingErrorCode.ChargeNotPayable, $"Khoản {command.ChargeId} không phải phí khám.");

            if (await cashier.FindExamReceiptAsync(command.BookingAttemptId, command.ChargeId, token) is { } previous)
                return new ExamFeeCashReceipt(previous.ReceiptNumber, previous.PaymentIntentId, command.BookingAttemptId, previous.PatientId,
                    command.ChargeId, previous.Amount, BillingCurrency.Vnd, previous.CollectedAtUtc, AlreadyCollected: true);
            if (charge.Outstanding <= 0m)
                throw new BillingException(BillingErrorCode.ChargeNotPayable,
                    $"Phí khám {command.ChargeId} đã được trả bằng giao dịch khác hoặc đã hủy.");
            // Lượt đặt lịch đã trả phí khám ở khoản khác (ví dụ trên VNPAY): thu nữa là trả trùng.
            if (await store.HasRetainedExamPaymentAsync(command.BookingAttemptId, Guid.Empty, token))
                throw new BillingException(BillingErrorCode.ChargeNotPayable,
                    $"Lượt đặt lịch {command.BookingAttemptId} đã được thanh toán phí khám.");

            var cash = await RecordCashAsync([charge], PaymentPurpose.InitialConsultation, command.PatientId, command.BookingAttemptId,
                encounterId: null, command.ReceptionistAppUserId, now, new { bookingAttemptId = command.BookingAttemptId }, command.ClientIp,
                token);
            return new ExamFeeCashReceipt(cash.ReceiptNumber, cash.PaymentIntentId, command.BookingAttemptId, command.PatientId,
                command.ChargeId, cash.Amount, BillingCurrency.Vnd, now, AlreadyCollected: false);
        }, ct);
    }

    /// <summary>Tiền mặt tiếp tân đã thu và đã hoàn trong ngày hôm nay (giờ Việt Nam), kèm danh sách phiếu để đối chiếu.</summary>
    public async Task<CashierShiftSummary> GetTodaySummaryAsync(Guid staffUserId, CancellationToken ct)
    {
        var now = await NowAsync(ct);
        var from = ClsBatches.StartOfDayUtc(now);
        var closure = await cashier.FindClosureAsync(staffUserId, DateOnly.FromDateTime(now.AddHours(7)), ct);
        var receipts = await cashier.GetReceiptsAsync(staffUserId, from, from.AddDays(1), ct);
        var refunds = await cashier.GetCashRefundsAsync(staffUserId, from, from.AddDays(1), ct);
        var collected = closure?.ExpectedCashCollected ?? receipts.Sum(r => r.Amount);
        var refunded = closure?.ExpectedCashRefunded ?? refunds.Sum(r => r.Amount);
        return new CashierShiftSummary(
            DateOnly.FromDateTime(from + TimeSpan.FromHours(7)),
            staffUserId,
            closure?.ReceiptCount ?? receipts.Count,
            collected,
            refunded,
            collected - refunded,
            BillingCurrency.Vnd,
            receipts,
            refunds, closure is not null, closure?.ClosedAtUtc, closure?.ActualCashCounted, closure?.DiscrepancyNote);
    }

    private Task<DateTime> NowAsync(CancellationToken ct) => dbClock?.GetUtcNowAsync(ct) ?? Task.FromResult(clock.GetUtcNow().UtcDateTime);
    private async Task<DateTime> OpenShiftTimeAsync(Guid actor, CancellationToken ct)
    {
        var now = await NowAsync(ct);
        var date = DateOnly.FromDateTime(now.AddHours(7));
        await cashier.EnsureShiftOpenAsync(actor, date, ct);
        now = await NowAsync(ct);
        if (DateOnly.FromDateTime(now.AddHours(7)) != date)
            throw new BillingException(BillingErrorCode.CashierShiftDateChanged, "Ngày thu ngân đã đổi; vui lòng gửi lại.");
        return now;
    }

    private sealed record RecordedCash(string ReceiptNumber, Guid PaymentIntentId, decimal Amount, int CancelledOnlineRequests);

    // Bên trong transaction, các khoản đã khóa và còn phải trả: hủy yêu cầu online đang mở, ghi phiếu thu (PaymentIntent Counter
    // Succeeded + PaymentTransaction Success + phân bổ), PaymentVerified vào Outbox và AuditLog.
    private async Task<RecordedCash> RecordCashAsync(IReadOnlyList<BillingItem> locked, PaymentPurpose purpose, Guid patientId,
        Guid? bookingAttemptId, Guid? encounterId, Guid staffUserId, DateTime now, object context, string? clientIp, CancellationToken token)
    {
        var ids = locked.Select(c => c.Id).ToList();
        var open = await store.FindOpenPaymentsAsync(ids, bookingAttemptId, now, token);
        foreach (var request in open)
            request.Intent.Status = PaymentIntentStatus.Cancelled;

        var amount = locked.Sum(c => c.Outstanding);
        var receiptNumber = $"PT{now + TimeSpan.FromHours(7):yyyyMMdd}{Guid.NewGuid():N}"[..22].ToUpperInvariant();
        var intent = new PaymentIntent
        {
            IntentNumber = $"PI-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..23].ToUpperInvariant(),
            PayerAppUserId = staffUserId,
            PatientId = patientId,
            BookingAttemptId = bookingAttemptId,
            EncounterId = encounterId,
            TotalExpectedAmount = amount,
            Currency = BillingCurrency.Vnd,
            Provider = PaymentIntentProvider.Counter,
            PaymentType = purpose == PaymentPurpose.InitialConsultation ? PaymentType.InitialConsultation : PaymentType.CLSOrderBatch,
            Status = PaymentIntentStatus.Succeeded,
            CreatedAtUtc = now,
            ExpiresAtUtc = now,
        };
        var transaction = new PaymentTransaction
        {
            TransactionReference = receiptNumber,
            TransactionAmount = amount,
            Currency = BillingCurrency.Vnd,
            PaymentStatus = PaymentStatus.Success,
            VerifiedAtUtc = now,
            ProviderRawData = JsonSerializer.Serialize(new { channel = "Counter", collectedBy = staffUserId, context }),
        };
        var allocations = locked
            .Select(c => new PaymentAllocation { BillingItemId = c.Id, AllocatedAmount = c.Outstanding, AllocatedAtUtc = now })
            .ToList();

        store.Add(intent, transaction, allocations);
        foreach (var allocation in allocations)
            locked.Single(c => c.Id == allocation.BillingItemId).ApplyPayment(allocation.AllocatedAmount);
        await store.SaveChangesAsync(token);

        await store.EnqueueAsync(new PaymentVerified(
            Guid.CreateVersion7(),
            intent.Id,
            purpose,
            patientId,
            bookingAttemptId,
            encounterId,
            locked.Where(c => c.Status == BillingItemStatus.Paid).Select(c => c.Id).ToList(),
            now,
            OperationId: null), token);

        await cashier.WriteCollectionAuditAsync(staffUserId, transaction.Id, JsonSerializer.Serialize(new
        {
            receiptNumber,
            amount,
            currency = BillingCurrency.Vnd,
            purpose = purpose.ToString(),
            context,
            patientId,
            chargeIds = ids,
            cancelledOnlineIntentIds = open.Select(o => o.Intent.Id).ToList(),
        }), clientIp, token, now);

        return new RecordedCash(receiptNumber, intent.Id, amount, open.Count);
    }
}
