using System.Text.Json;
using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

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
/// <param name="CashRefunded">Tiền mặt đã hoàn tại quầy; luôn 0 cho tới khi có luồng hoàn tiền.</param>
public sealed record CashierShiftSummary(
    DateOnly ShiftDate,
    Guid CashierUserId,
    int ReceiptCount,
    decimal CashCollected,
    decimal CashRefunded,
    decimal NetCash,
    string Currency,
    IReadOnlyList<CounterReceipt> Receipts);

/// <summary>
/// Thu tiền mặt tại quầy (Giải pháp kỹ thuật v2.13 mục 5.5), hiện cho phí CLS theo đợt chỉ định. Tiếp tân xác nhận đã nhận
/// tiền thì ghi nhận đã xác minh ngay: PaymentIntent Provider = Counter, PaymentTransaction Success với số phiếu thu làm
/// TransactionReference, phân bổ vào từng khoản, PaymentVerified vào Outbox và AuditLog trong cùng một transaction.
/// Với Provider = Counter, PaymentIntent.PayerAppUserId là tài khoản tiếp tân đã nhận tiền; đối soát ca dựa trên trường này.
/// </summary>
public sealed class CashierService(IChargeStore charges, IPaymentStore store, ICashierStore cashier, TimeProvider clock)
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

        var items = await charges.GetClsByServiceOrderAsync(serviceOrderId, ct);
        if (items.Count == 0 || items.Any(i => i.PatientId is null || i.EncounterId is null)
            || items.Select(i => i.PatientId).Distinct().Count() != 1)
            return new CashCollection(CashCollectionOutcome.NotFound, null);

        var now = clock.GetUtcNow().UtcDateTime;
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

            var open = await store.FindOpenPaymentsAsync(ids, null, now, token);
            foreach (var request in open)
                request.Intent.Status = PaymentIntentStatus.Cancelled;

            var amount = locked.Sum(c => c.Outstanding);
            var receiptNumber = $"PT{now + TimeSpan.FromHours(7):yyyyMMdd}{Guid.NewGuid():N}"[..22].ToUpperInvariant();
            var intent = new PaymentIntent
            {
                IntentNumber = $"PI-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..23].ToUpperInvariant(),
                PayerAppUserId = staffUserId,
                PatientId = batch.PatientId,
                EncounterId = batch.EncounterId,
                TotalExpectedAmount = amount,
                Currency = BillingCurrency.Vnd,
                Provider = PaymentIntentProvider.Counter,
                PaymentType = PaymentType.CLSOrderBatch,
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
                ProviderRawData = JsonSerializer.Serialize(new { channel = "Counter", collectedBy = staffUserId, serviceOrderId }),
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
                PaymentPurpose.CLSOrderBatch,
                batch.PatientId,
                BookingAttemptId: null,
                batch.EncounterId,
                locked.Where(c => c.Status == BillingItemStatus.Paid).Select(c => c.Id).ToList(),
                now,
                OperationId: null), token);

            await cashier.WriteCollectionAuditAsync(staffUserId, transaction.Id, JsonSerializer.Serialize(new
            {
                receiptNumber,
                amount,
                currency = BillingCurrency.Vnd,
                serviceOrderId,
                patientId = batch.PatientId,
                chargeIds = ids,
                cancelledOnlineIntentIds = open.Select(o => o.Intent.Id).ToList(),
            }), clientIp, token);

            return new CashReceipt(receiptNumber, intent.Id, serviceOrderId, batch.PatientId, batch.EncounterId, amount,
                BillingCurrency.Vnd, now, open.Count);
        }, ct);

        return new CashCollection(CashCollectionOutcome.Collected, receipt);
    }

    /// <summary>Tổng tiền mặt tiếp tân đã thu trong ngày hôm nay (giờ Việt Nam), kèm danh sách phiếu để đối chiếu.</summary>
    public async Task<CashierShiftSummary> GetTodaySummaryAsync(Guid staffUserId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var from = ClsBatches.StartOfDayUtc(now);
        var receipts = await cashier.GetReceiptsAsync(staffUserId, from, from.AddDays(1), ct);
        var collected = receipts.Sum(r => r.Amount);
        return new CashierShiftSummary(
            DateOnly.FromDateTime(from + TimeSpan.FromHours(7)),
            staffUserId,
            receipts.Count,
            collected,
            CashRefunded: 0m,
            NetCash: collected,
            BillingCurrency.Vnd,
            receipts);
    }
}
