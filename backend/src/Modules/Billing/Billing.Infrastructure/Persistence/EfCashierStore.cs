using Billing.Application;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;
using Shared.Infrastructure.Persistence.Entities;

namespace Billing.Infrastructure.Persistence;

internal sealed class EfCashierStore(BillingDbContext db, SharedDbContext shared, TimeProvider clock) : ICashierStore
{
    public const string RefundTransactionTable = "billing.RefundTransaction";

    public Task WriteCollectionAuditAsync(Guid staffUserId, Guid paymentTransactionId, string newValuesJson, string? clientIp,
        CancellationToken ct) =>
        BillingAuditWriter.WriteAsync(db, shared, new BillingAuditEntry(staffUserId, "billing.PaymentTransaction", paymentTransactionId,
            IsInsert: true, "PaymentStatus,TransactionAmount,VerifiedAtUtc", newValuesJson, "Thu tiền mặt tại quầy", clientIp),
            clock.GetUtcNow().UtcDateTime, ct);

    // Với Provider = Counter, PayerAppUserId là tài khoản tiếp tân đã nhận tiền (xem CashierService).
    public async Task<IReadOnlyList<CounterReceipt>> GetReceiptsAsync(Guid staffUserId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await (
                from intent in db.Set<PaymentIntent>().AsNoTracking()
                join transaction in db.Set<PaymentTransaction>().AsNoTracking() on intent.Id equals transaction.PaymentIntentId
                where intent.Provider == PaymentIntentProvider.Counter && intent.PayerAppUserId == staffUserId
                      && transaction.PaymentStatus == PaymentStatus.Success
                      && transaction.VerifiedAtUtc >= fromUtc && transaction.VerifiedAtUtc < toUtc
                orderby transaction.VerifiedAtUtc
                select new CounterReceipt(transaction.TransactionReference, intent.Id, intent.PatientId, intent.EncounterId,
                    transaction.TransactionAmount, transaction.VerifiedAtUtc!.Value))
            .ToListAsync(ct);

    public Task<CounterReceipt?> FindExamReceiptAsync(Guid bookingAttemptId, Guid chargeId, CancellationToken ct) =>
        (
            from intent in db.Set<PaymentIntent>().AsNoTracking()
            join transaction in db.Set<PaymentTransaction>().AsNoTracking() on intent.Id equals transaction.PaymentIntentId
            join allocation in db.Set<PaymentAllocation>().AsNoTracking() on transaction.Id equals allocation.PaymentTransactionId
            where intent.BookingAttemptId == bookingAttemptId && intent.Provider == PaymentIntentProvider.Counter
                  && transaction.PaymentStatus == PaymentStatus.Success && allocation.BillingItemId == chargeId
            orderby transaction.VerifiedAtUtc
            select new CounterReceipt(transaction.TransactionReference, intent.Id, intent.PatientId, intent.EncounterId,
                transaction.TransactionAmount, transaction.VerifiedAtUtc!.Value))
        .FirstOrDefaultAsync(ct);

    // RefundTransaction không có cột người chi; người chi lấy từ AuditLog ghi cùng transaction với phiếu hoàn (RefundService).
    public async Task<IReadOnlyList<CashRefundSlip>> GetCashRefundsAsync(Guid staffUserId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var ids = await shared.Set<AuditLog>().AsNoTracking()
            .Where(a => a.TableName == RefundTransactionTable && a.PerformedByUserId == staffUserId
                        && a.TimestampUtc >= fromUtc && a.TimestampUtc < toUtc)
            .Select(a => a.RecordId)
            .ToListAsync(ct);
        if (ids.Count == 0)
            return [];

        return await (
                from refund in db.Set<RefundTransaction>().AsNoTracking()
                join original in db.Set<PaymentTransaction>().AsNoTracking() on refund.OriginalPaymentTransactionId equals original.Id
                join intent in db.Set<PaymentIntent>().AsNoTracking() on original.PaymentIntentId equals intent.Id
                where ids.Contains(refund.Id) && refund.Status == RefundTransactionStatus.Success
                orderby refund.ProcessedAtUtc
                select new CashRefundSlip(refund.ProviderRefundTransactionId!, refund.RefundRequestId, intent.PatientId, refund.RefundedAmount,
                    refund.Currency, refund.ProcessedAtUtc))
            .ToListAsync(ct);
    }
}
