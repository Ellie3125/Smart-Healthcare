using Billing.Contracts;
using Billing.Domain.Entities;
using Billing.Domain.Enums;

namespace Billing.Application;

/// <summary>
/// Tạo yêu cầu thanh toán và ghi nhận thanh toán đã xác minh (contract v0.2, đề xuất cho câu hỏi mở 1 và 2).
/// Không gọi cổng thanh toán: adapter VNPAY/MoMo gọi cổng sau khi yêu cầu đã commit, và gọi ConfirmVerifiedAsync
/// sau khi đã kiểm chữ ký IPN.
/// </summary>
public sealed class PaymentService(IPaymentStore store, TimeProvider clock)
{
    public Task<PaymentDto> CreateAsync(CreatePayment command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow().UtcDateTime;
        Validate(command, now);
        var chargeIds = command.ChargeIds.Order().ToList();

        return store.InTransactionAsync(async token =>
        {
            if (command.BookingAttemptId is { } attemptId)
                await store.LockBookingAttemptAsync(attemptId, token);

            var charges = await store.LockChargesAsync(chargeIds, token);
            EnsurePayable(command, chargeIds, charges);

            var open = await store.FindOpenPaymentsAsync(chargeIds, command.BookingAttemptId, now, token);
            if (open.Count == 1 && SameRequest(open[0], command, chargeIds))
                return ToDto(open[0].Intent, open[0].Transaction, open[0].ChargeIds);
            if (open.Count > 0)
                throw new BillingException(BillingErrorCode.PaymentConflict,
                    $"Đã có yêu cầu thanh toán đang mở ({string.Join(", ", open.Select(o => o.Intent.IntentNumber))}) cho khoản hoặc lượt đặt lịch này.");

            var amount = charges.Sum(c => c.Outstanding);
            var intent = new PaymentIntent
            {
                IntentNumber = $"PI-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..23].ToUpperInvariant(),
                PayerAppUserId = command.PayerAppUserId,
                PatientId = command.PatientId,
                BookingAttemptId = command.BookingAttemptId,
                EncounterId = command.EncounterId,
                TotalExpectedAmount = amount,
                Currency = BillingCurrency.Vnd,
                Provider = ToDomain(command.Provider),
                PaymentType = command.Purpose == PaymentPurpose.InitialConsultation ? PaymentType.InitialConsultation : PaymentType.CLSOrderBatch,
                Status = PaymentIntentStatus.Pending,
                CreatedAtUtc = now,
                ExpiresAtUtc = command.ExpiresAtUtc,
            };
            var transaction = new PaymentTransaction
            {
                // Chỉ chữ và số: hợp lệ cho vnp_TxnRef của VNPAY và orderId của MoMo.
                TransactionReference = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                TransactionAmount = amount,
                Currency = BillingCurrency.Vnd,
                PaymentStatus = PaymentStatus.Pending,
            };
            var allocations = charges
                .Select(c => new PaymentAllocation { BillingItemId = c.Id, AllocatedAmount = c.Outstanding, AllocatedAtUtc = now })
                .ToList();

            store.Add(intent, transaction, allocations);
            await store.SaveChangesAsync(token);
            return ToDto(intent, transaction, chargeIds);
        }, ct);
    }

    public Task<PaymentConfirmation> ConfirmVerifiedAsync(VerifiedPayment payment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (string.IsNullOrWhiteSpace(payment.TransactionReference) || string.IsNullOrWhiteSpace(payment.ProviderTransactionId))
            throw Invalid("Thiếu mã tham chiếu hoặc mã giao dịch của cổng thanh toán.");
        Guard.Utc(payment.VerifiedAtUtc, nameof(payment.VerifiedAtUtc));

        return store.InTransactionAsync(async token =>
        {
            var transaction = await store.LockTransactionAsync(payment.TransactionReference, token)
                ?? throw new BillingException(BillingErrorCode.PaymentNotFound, $"Không có giao dịch {payment.TransactionReference}.");

            if (transaction.PaymentStatus == PaymentStatus.Success)
            {
                if (transaction.ProviderTransactionId == payment.ProviderTransactionId)
                    return new PaymentConfirmation(transaction.PaymentIntentId, AlreadyRecorded: true, []);
                throw new BillingException(BillingErrorCode.PaymentConflict,
                    $"Giao dịch {payment.TransactionReference} đã được ghi nhận bằng mã cổng khác.");
            }

            if (payment.Amount != transaction.TransactionAmount
                || !string.Equals(payment.Currency, transaction.Currency, StringComparison.Ordinal))
                throw new BillingException(BillingErrorCode.AmountMismatch,
                    $"Cổng báo {payment.Amount} {payment.Currency}, giao dịch {payment.TransactionReference} cần {transaction.TransactionAmount} {transaction.Currency}.");

            if (await store.IsProviderTransactionIdTakenAsync(payment.ProviderTransactionId, transaction.Id, token))
                throw new BillingException(BillingErrorCode.PaymentConflict,
                    $"Mã giao dịch cổng {payment.ProviderTransactionId} đã được ghi nhận cho giao dịch khác.");

            var now = clock.GetUtcNow().UtcDateTime;
            transaction.PaymentStatus = PaymentStatus.Success;
            transaction.ProviderTransactionId = payment.ProviderTransactionId;
            transaction.VerifiedAtUtc = payment.VerifiedAtUtc;
            transaction.ProviderRawData = payment.ProviderRawData;

            // Tiền đã thu thật nên vẫn ghi nhận khi yêu cầu đã hết hạn; Scheduling xử lý PaidUnallocated (BR-003).
            var intent = await store.GetIntentAsync(transaction.PaymentIntentId, token);
            intent.Status = PaymentIntentStatus.Succeeded;

            var allocations = await store.GetAllocationsAsync(transaction.Id, token);
            var charges = await store.LockChargesAsync(allocations.Select(a => a.BillingItemId).Order().ToList(), token);
            foreach (var allocation in allocations)
                charges.Single(c => c.Id == allocation.BillingItemId).ApplyPayment(allocation.AllocatedAmount);

            var paidChargeIds = charges.Where(c => c.Status == BillingItemStatus.Paid).Select(c => c.Id).ToList();
            await store.SaveChangesAsync(token);
            await store.EnqueueAsync(new PaymentVerified(
                Guid.CreateVersion7(),
                intent.Id,
                intent.PaymentType == PaymentType.InitialConsultation ? PaymentPurpose.InitialConsultation : PaymentPurpose.CLSOrderBatch,
                intent.PatientId,
                intent.BookingAttemptId,
                intent.EncounterId,
                paidChargeIds,
                now,
                OperationId: null), token);

            return new PaymentConfirmation(intent.Id, AlreadyRecorded: false, paidChargeIds);
        }, ct);
    }

    private static void Validate(CreatePayment c, DateTime now)
    {
        if (c.PatientId == Guid.Empty || c.PayerAppUserId == Guid.Empty)
            throw Invalid("Thiếu PatientId hoặc PayerAppUserId.");
        if (c.ChargeIds is not { Count: > 0 } || c.ChargeIds.Distinct().Count() != c.ChargeIds.Count)
            throw Invalid("Danh sách khoản thu rỗng hoặc bị trùng.");
        Guard.Utc(c.ExpiresAtUtc, nameof(c.ExpiresAtUtc));
        if (c.ExpiresAtUtc <= now)
            throw Invalid("Hạn thanh toán phải sau thời điểm hiện tại.");
        switch (c.Purpose)
        {
            case PaymentPurpose.InitialConsultation when c.BookingAttemptId is null || c.EncounterId is not null:
                throw Invalid("Thanh toán phí khám cần BookingAttemptId và không mang EncounterId.");
            case PaymentPurpose.CLSOrderBatch when c.EncounterId is null || c.BookingAttemptId is not null:
                throw Invalid("Thanh toán phí CLS cần EncounterId và không mang BookingAttemptId.");
        }
    }

    private static void EnsurePayable(CreatePayment c, IReadOnlyList<Guid> requested, IReadOnlyList<BillingItem> charges)
    {
        var missing = requested.Except(charges.Select(x => x.Id)).ToList();
        if (missing.Count > 0)
            throw NotPayable($"Không tìm thấy khoản thu {string.Join(", ", missing)}.");

        var expectedType = c.Purpose == PaymentPurpose.InitialConsultation ? ItemType.ConsultationFee : ItemType.CLSService;
        foreach (var charge in charges)
        {
            if (charge.ItemType != expectedType)
                throw NotPayable($"Khoản {charge.Id} là {charge.ItemType}, không thuộc loại thanh toán {c.Purpose}.");
            if (c.Purpose == PaymentPurpose.CLSOrderBatch && charge.EncounterId != c.EncounterId)
                throw NotPayable($"Khoản {charge.Id} không thuộc lượt khám {c.EncounterId}.");
            if (charge.Outstanding <= 0m)
                throw NotPayable($"Khoản {charge.Id} đã trả đủ, đã hủy hoặc được miễn.");
        }
    }

    private static bool SameRequest(OpenPayment open, CreatePayment c, IReadOnlyList<Guid> chargeIds) =>
        open.Intent.Provider == ToDomain(c.Provider) && open.ChargeIds.Order().SequenceEqual(chargeIds);

    private static PaymentIntentProvider ToDomain(PaymentProvider provider) => provider switch
    {
        PaymentProvider.VNPAY => PaymentIntentProvider.VNPAY,
        PaymentProvider.MoMo => PaymentIntentProvider.MoMo,
        _ => throw Invalid($"Cổng thanh toán {provider} không hỗ trợ."),
    };

    private static PaymentDto ToDto(PaymentIntent i, PaymentTransaction t, IReadOnlyList<Guid> chargeIds) => new(
        i.Id,
        i.IntentNumber,
        t.TransactionReference,
        i.PaymentType == PaymentType.InitialConsultation ? PaymentPurpose.InitialConsultation : PaymentPurpose.CLSOrderBatch,
        i.Provider == PaymentIntentProvider.MoMo ? PaymentProvider.MoMo : PaymentProvider.VNPAY,
        t.TransactionAmount,
        t.Currency,
        i.Status switch
        {
            PaymentIntentStatus.Pending => PaymentState.Pending,
            PaymentIntentStatus.Processing => PaymentState.Processing,
            PaymentIntentStatus.Succeeded => PaymentState.Succeeded,
            PaymentIntentStatus.Failed => PaymentState.Failed,
            PaymentIntentStatus.Unknown => PaymentState.Unknown,
            PaymentIntentStatus.Expired => PaymentState.Expired,
            PaymentIntentStatus.Cancelled => PaymentState.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(i), i.Status, null),
        },
        i.ExpiresAtUtc,
        chargeIds.Order().ToList());

    private static BillingException Invalid(string message) => new(BillingErrorCode.InvalidRequest, message);

    private static BillingException NotPayable(string message) => new(BillingErrorCode.ChargeNotPayable, message);
}
