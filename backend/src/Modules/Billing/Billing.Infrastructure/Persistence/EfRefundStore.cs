using Billing.Application;
using Billing.Domain.Entities;
using Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence;

namespace Billing.Infrastructure.Persistence;

internal sealed class EfRefundStore(BillingDbContext db, SharedDbContext shared, TimeProvider clock) : IRefundStore
{
    public async Task<IReadOnlyList<RefundablePayment>> GetPaidAllocationsAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct)
    {
        if (chargeIds.Count == 0)
            return [];
        var ids = chargeIds.ToList();
        return await (
                from intent in db.Set<PaymentIntent>().AsNoTracking()
                join transaction in db.Set<PaymentTransaction>().AsNoTracking() on intent.Id equals transaction.PaymentIntentId
                join allocation in db.Set<PaymentAllocation>().AsNoTracking() on transaction.Id equals allocation.PaymentTransactionId
                where transaction.PaymentStatus == PaymentStatus.Success && ids.Contains(allocation.BillingItemId)
                select new RefundablePayment(transaction.Id, intent.Id, allocation.BillingItemId, allocation.AllocatedAmount))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RefundablePayment>> GetPaidAllocationsOfIntentAsync(Guid paymentIntentId, CancellationToken ct) =>
        await (
                from intent in db.Set<PaymentIntent>().AsNoTracking()
                join transaction in db.Set<PaymentTransaction>().AsNoTracking() on intent.Id equals transaction.PaymentIntentId
                join allocation in db.Set<PaymentAllocation>().AsNoTracking() on transaction.Id equals allocation.PaymentTransactionId
                where transaction.PaymentStatus == PaymentStatus.Success && intent.Id == paymentIntentId
                select new RefundablePayment(transaction.Id, intent.Id, allocation.BillingItemId, allocation.AllocatedAmount))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RefundRequest>> GetActiveRequestsAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct)
    {
        if (chargeIds.Count == 0)
            return [];
        return await db.Set<RefundRequest>().AsNoTracking()
            .Where(r => chargeIds.Contains(r.BillingItemId)
                        && (r.Status == RefundRequestStatus.Pending || r.Status == RefundRequestStatus.Approved
                            || r.Status == RefundRequestStatus.Settled))
            .ToListAsync(ct);
    }

    public Task<RefundRequest?> LockRequestAsync(Guid refundRequestId, CancellationToken ct) =>
        db.Set<RefundRequest>()
            .FromSql($"SELECT * FROM [billing].[RefundRequest] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {refundRequestId}")
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RefundView>> ListAsync(RefundQuery query, CancellationToken ct) =>
        await Views(query.Status, query.PatientId, null).Take(query.Take).ToListAsync(ct);

    public Task<RefundView?> GetAsync(Guid refundRequestId, CancellationToken ct) =>
        Views(null, null, refundRequestId).SingleOrDefaultAsync(ct);

    public void Add(RefundRequest request) => db.Add(request);

    public void Add(RefundTransaction transaction) => db.Add(transaction);

    public Task WriteAuditAsync(BillingAuditEntry entry, CancellationToken ct) =>
        BillingAuditWriter.WriteAsync(db, shared, entry, clock.GetUtcNow().UtcDateTime, ct);

    // Lọc và sắp trước khi chiếu sang RefundView: EF không dịch được truy cập thuộc tính của record đã chiếu. Mới nhất trước.
    private IQueryable<RefundView> Views(RefundRequestStatus? status, Guid? patientId, Guid? refundRequestId) =>
        from request in db.Set<RefundRequest>().AsNoTracking()
        join item in db.Set<BillingItem>().AsNoTracking() on request.BillingItemId equals item.Id
        join original in db.Set<PaymentTransaction>().AsNoTracking() on request.OriginalPaymentTransactionId equals original.Id
        join intent in db.Set<PaymentIntent>().AsNoTracking() on original.PaymentIntentId equals intent.Id
        join refund in db.Set<RefundTransaction>().AsNoTracking() on request.Id equals refund.RefundRequestId into refunds
        from refund in refunds.DefaultIfEmpty()
        where (status == null || request.Status == status) && (patientId == null || intent.PatientId == patientId)
              && (refundRequestId == null || request.Id == refundRequestId)
        orderby request.RequestedAtUtc descending, request.Id
        select new RefundView(
            request.Id,
            request.Status.ToString(),
            request.RefundReason.ToString(),
            request.PolicyRuleApplied.ToString(),
            request.RequestedAmount,
            original.Currency,
            item.Id,
            item.ItemNameSnapshot,
            intent.PatientId,
            request.AppointmentId,
            original.TransactionReference,
            intent.Provider.ToString(),
            RefundService.CashCounter,
            request.RequestedAtUtc,
            request.DecidedAtUtc,
            request.DecisionByStaffId,
            refund != null ? refund.ProviderRefundTransactionId : null,
            refund != null ? (DateTime?)refund.ProcessedAtUtc : null);
}
